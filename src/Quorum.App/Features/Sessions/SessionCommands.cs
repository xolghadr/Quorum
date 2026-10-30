using Microsoft.EntityFrameworkCore;
using Quorum.App.Features.Groups;
using Quorum.App.Features.Shared;
using Quorum.App.Infrastructure.Logging;
using Quorum.App.Infrastructure.Persistence;
using Quorum.App.SharedKernel;

namespace Quorum.App.Features.Sessions;

public sealed record TopicChoice(Guid Id, string Title, string Key, int PickCount, int TrackSort, int TopicSort);

public sealed class SessionCommands
{
    private readonly QuorumDbContext _db;
    private readonly IClock _clock;
    private readonly SafeLog _log;

    public SessionCommands(QuorumDbContext db, IClock clock, SafeLog log)
    {
        _db = db;
        _clock = clock;
        _log = log;
    }

    public async Task<CommandOutcome> CreateAsync(
        GroupId groupId,
        AccountId accountId,
        IReadOnlyList<Guid> topicIds,
        CancellationToken cancellationToken = default)
    {
        if (!await _db.CatalogPublications.AnyAsync(cancellationToken))
            return CommandOutcome.Refused();

        var actor = await FindActor(groupId, accountId, cancellationToken);
        if (actor is null)
            return CommandOutcome.NotFound();

        var existingNumbers = await _db.Sessions
            .Where(session => session.GroupId == groupId)
            .Select(session => session.Number)
            .ToListAsync(cancellationToken);
        var isFirst = existingNumbers.Count == 0;
        if (isFirst)
        {
            if (actor.Group.CreatorAccountId != accountId)
                return CommandOutcome.Refused();
            if (!await TopicListValid(topicIds, cancellationToken))
                return CommandOutcome.Refused();
        }
        else if (topicIds.Count > 0)
        {
            return CommandOutcome.Refused();
        }

        var session = new Session
        {
            Id = SessionId.New(),
            GroupId = groupId,
            Number = existingNumbers.Count == 0 ? 1 : existingNumbers.Max() + 1,
            CreatedByAccountId = accountId,
            CreatedOn = _clock.Today,
        };
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        _db.Sessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);
        if (isFirst)
        {
            await AddTopics(_db, groupId, session.Id, topicIds, previous: [], cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        _log.Command("create-session", accountId, groupId, session.Id);
        return CommandOutcome.Ok(session.Id.Value);
    }

    public async Task<CommandOutcome> SaveTopicsAsync(
        GroupId groupId,
        AccountId accountId,
        SessionId sessionId,
        IReadOnlyList<Guid> topicIds,
        CancellationToken cancellationToken = default)
    {
        var actor = await FindActor(groupId, accountId, cancellationToken);
        if (actor is null)
            return CommandOutcome.NotFound();

        var session = await _db.Sessions.FirstOrDefaultAsync(
            row => row.Id == sessionId && row.GroupId == groupId,
            cancellationToken);
        if (session is null)
            return CommandOutcome.NotFound();

        if (!await TopicListValid(topicIds, cancellationToken))
            return CommandOutcome.Refused();

        if (session.Number == 1)
        {
            if (session.FacilitatorMembershipId is not null || actor.Group.CreatorAccountId != accountId)
                return CommandOutcome.Refused();
        }
        else if (session.FacilitatorMembershipId is null)
        {
            return CommandOutcome.Refused();
        }

        var previous = await _db.SessionTopics
            .Where(topic => topic.SessionId == sessionId)
            .ToListAsync(cancellationToken);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        _db.SessionTopics.RemoveRange(previous);
        await _db.SaveChangesAsync(cancellationToken);
        await AddTopics(_db, groupId, sessionId, topicIds, previous, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        _log.Command("save-topics", accountId, groupId, sessionId);
        return CommandOutcome.Ok();
    }

    public static async Task<IReadOnlyList<TopicChoice>> ChooserAsync(
        QuorumDbContext db,
        GroupId groupId,
        CancellationToken cancellationToken = default)
    {
        var topics = await db.Topics
            .Join(db.Tracks, topic => topic.TrackId, track => track.Id, (topic, track) => new { topic, track })
            .Select(row => new
            {
                row.topic.Id,
                row.topic.Title,
                row.topic.Key,
                row.topic.SortOrder,
                TrackSort = row.track.SortOrder,
            })
            .ToListAsync(cancellationToken);
        var counts = await db.TopicProgress
            .Where(progress => progress.GroupId == groupId)
            .ToDictionaryAsync(progress => progress.TopicId, progress => progress.PickCount, cancellationToken);
        return topics
            .Select(topic => new TopicChoice(
                topic.Id,
                topic.Title,
                topic.Key,
                counts.GetValueOrDefault(topic.Id),
                topic.TrackSort,
                topic.SortOrder))
            .OrderBy(topic => topic.PickCount == 0 ? 0 : 1)
            .ThenBy(topic => topic.TrackSort)
            .ThenBy(topic => topic.TopicSort)
            .ToList();
    }

    public static async Task<bool> TrackIsReviewed(
        QuorumDbContext db,
        GroupId groupId,
        Guid trackId,
        CancellationToken cancellationToken = default)
    {
        var topicIds = await db.Topics
            .Where(topic => topic.TrackId == trackId)
            .Select(topic => topic.Id)
            .ToListAsync(cancellationToken);
        if (topicIds.Count == 0)
            return false;

        var picked = await db.TopicProgress.CountAsync(
            progress => progress.GroupId == groupId && topicIds.Contains(progress.TopicId) && progress.PickCount >= 1,
            cancellationToken);
        return picked == topicIds.Count;
    }

    private async Task<Actor?> FindActor(GroupId groupId, AccountId accountId, CancellationToken cancellationToken)
    {
        var group = await _db.Groups.FirstOrDefaultAsync(row => row.Id == groupId, cancellationToken);
        if (group is null)
            return null;
        var membership = await _db.Memberships.FirstOrDefaultAsync(
            member => member.GroupId == groupId && member.AccountId == accountId && member.RemovedAt == null,
            cancellationToken);
        return membership is null ? null : new Actor(group, membership);
    }

    private async Task<bool> TopicListValid(IReadOnlyList<Guid> topicIds, CancellationToken cancellationToken)
    {
        if (topicIds.Count is < 2 or > 3)
            return false;
        if (topicIds.Distinct().Count() != topicIds.Count)
            return false;
        var known = await _db.Topics.CountAsync(topic => topicIds.Contains(topic.Id), cancellationToken);
        return known == topicIds.Count;
    }

    private static async Task AddTopics(
        QuorumDbContext db,
        GroupId groupId,
        SessionId sessionId,
        IReadOnlyList<Guid> topicIds,
        IReadOnlyList<SessionTopic> previous,
        CancellationToken cancellationToken)
    {
        var already = previous.Select(topic => topic.TopicId).ToHashSet();
        var position = 1;
        foreach (var topicId in topicIds)
        {
            db.SessionTopics.Add(new SessionTopic
            {
                SessionId = sessionId,
                TopicId = topicId,
                Position = position++,
            });
            if (already.Contains(topicId))
                continue;

            var progress = await db.TopicProgress.FirstOrDefaultAsync(
                row => row.GroupId == groupId && row.TopicId == topicId,
                cancellationToken);
            if (progress is null)
            {
                db.TopicProgress.Add(new TopicProgress
                {
                    Id = Guid.CreateVersion7(),
                    GroupId = groupId,
                    TopicId = topicId,
                    PickCount = 1,
                });
            }
            else
            {
                progress.PickCount += 1;
            }
        }
    }

    private sealed record Actor(Group Group, Membership Membership);
}
