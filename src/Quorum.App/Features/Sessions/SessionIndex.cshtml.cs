using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quorum.App.Features.Shared;
using Quorum.App.Infrastructure.Persistence;

namespace Quorum.App.Features.Sessions;

public sealed class SessionIndexModel : SignedInPageModel
{
    private readonly QuorumDbContext _db;
    private readonly SessionCommands _sessions;

    public SessionIndexModel(QuorumDbContext db, SessionCommands sessions)
    {
        _db = db;
        _sessions = sessions;
    }

    public bool Published { get; private set; }
    public bool IsCreator { get; private set; }
    public bool CanCreate { get; private set; }
    public bool HasSessions { get; private set; }
    public IReadOnlyList<SessionRow> Sessions { get; private set; } = [];
    public IReadOnlyList<TopicChoice> Chooser { get; private set; } = [];
    public string? Error { get; set; }

    [BindProperty]
    public string? Topic1 { get; set; }

    [BindProperty]
    public string? Topic2 { get; set; }

    [BindProperty]
    public string? Topic3 { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var gate = await RequireGroupAsync(_db, cancellationToken);
        if (gate is not null)
            return gate;
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var gate = await RequireGroupAsync(_db, cancellationToken);
        if (gate is not null)
            return gate;

        var topicIds = TopicIds();
        var result = await _sessions.CreateAsync(OpenGroupId!.Value, AccountId, topicIds, cancellationToken);
        if (!result.Succeeded || result.Id is null)
        {
            Error = "That change was not saved.";
            await LoadAsync(cancellationToken);
            return Page();
        }

        return Redirect($"/sessions/{result.Id}");
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Published = await _db.CatalogPublications.AnyAsync(cancellationToken);
        var group = await _db.Groups.FirstAsync(row => row.Id == OpenGroupId, cancellationToken);
        IsCreator = group.CreatorAccountId == AccountId;
        if (!Published)
            return;

        Chooser = await SessionCommands.ChooserAsync(_db, OpenGroupId!.Value, cancellationToken);
        var sessions = await _db.Sessions
            .Where(session => session.GroupId == OpenGroupId)
            .OrderBy(session => session.Number)
            .ToListAsync(cancellationToken);
        HasSessions = sessions.Count > 0;
        var waiting = sessions.Any(session => session.FacilitatorMembershipId is null);
        var latestFacilitator = sessions
            .Where(session => session.FacilitatorMembershipId is not null)
            .OrderByDescending(session => session.Number)
            .Select(session => session.FacilitatorMembershipId)
            .FirstOrDefault();
        var self = await _db.Memberships.FirstOrDefaultAsync(
            member => member.GroupId == OpenGroupId && member.AccountId == AccountId && member.RemovedAt == null,
            cancellationToken);
        CanCreate = Published && !waiting && (IsCreator || (self is not null && latestFacilitator == self.Id));
        var rows = new List<SessionRow>();
        foreach (var session in sessions)
        {
            var topics = await (
                from link in _db.SessionTopics
                join topic in _db.Topics on link.TopicId equals topic.Id
                where link.SessionId == session.Id
                orderby link.Position
                select new SessionTopicLink(topic.Key, topic.Title)).ToListAsync(cancellationToken);
            string? facilitator = null;
            if (session.FacilitatorMembershipId is { } facilitatorId)
            {
                facilitator = await _db.Memberships
                    .Where(member => member.Id == facilitatorId)
                    .Select(member => member.DisplayName)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            rows.Add(new SessionRow(session.Id.Value, session.Number, session.CreatedOn, topics, facilitator));
        }

        Sessions = rows;
    }

    private List<Guid> TopicIds()
    {
        var ids = new List<Guid>();
        foreach (var value in new[] { Topic1, Topic2, Topic3 })
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;
            ids.Add(Guid.TryParse(value, out var id) ? id : Guid.Empty);
        }

        return ids;
    }

    public sealed record SessionTopicLink(string Key, string Title);
    public sealed record SessionRow(Guid Id, int Number, DateOnly CreatedOn, IReadOnlyList<SessionTopicLink> Topics, string? Facilitator);
}
