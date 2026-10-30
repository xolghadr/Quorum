using Microsoft.EntityFrameworkCore;
using Quorum.App.Features.Shared;
using Quorum.App.Infrastructure.Logging;
using Quorum.App.Infrastructure.Persistence;
using Quorum.App.SharedKernel;

namespace Quorum.App.Features.Groups;

public sealed class RosterCommands
{
    public const string InvalidNameMessage = "Enter a display name of 1 to 40 characters that is not already in use.";
    public const string GroupFullMessage = "A group can have at most 8 members.";

    private readonly QuorumDbContext _db;
    private readonly IClock _clock;
    private readonly SafeLog _log;

    public RosterCommands(QuorumDbContext db, IClock clock, SafeLog log)
    {
        _db = db;
        _clock = clock;
        _log = log;
    }

    public async Task<CommandOutcome> AddAsync(
        GroupId groupId,
        AccountId actorId,
        string? displayName,
        CancellationToken cancellationToken = default)
    {
        var group = await OwnedGroup(groupId, actorId, cancellationToken);
        if (group is null)
            return CommandOutcome.NotFound();
        if (!Membership.DisplayNameAccepted(displayName, out var shown))
            return CommandOutcome.Refused();

        var active = await Active(groupId, cancellationToken);
        if (active.Count >= Group.MemberMax)
            return CommandOutcome.Refused();
        if (active.Any(member => member.DisplayNameKey == Membership.KeyFor(shown)))
            return CommandOutcome.Refused();

        _db.Memberships.Add(new Membership
        {
            Id = MemberId.New(),
            GroupId = groupId,
            DisplayName = shown,
            DisplayNameKey = Membership.KeyFor(shown),
            CreatedAt = _clock.UtcNow,
        });
        await _db.SaveChangesAsync(cancellationToken);
        _log.Command("add-member", actorId, groupId);
        return CommandOutcome.Ok();
    }

    public async Task<CommandOutcome> RenameAsync(
        GroupId groupId,
        AccountId actorId,
        MemberId memberId,
        string? displayName,
        CancellationToken cancellationToken = default)
    {
        var group = await OwnedGroup(groupId, actorId, cancellationToken);
        if (group is null)
            return CommandOutcome.NotFound();
        if (!Membership.DisplayNameAccepted(displayName, out var shown))
            return CommandOutcome.Refused();

        var membership = await _db.Memberships.FirstOrDefaultAsync(
            member => member.Id == memberId && member.GroupId == groupId && member.RemovedAt == null,
            cancellationToken);
        if (membership is null)
            return CommandOutcome.NotFound();

        var key = Membership.KeyFor(shown);
        var collision = await _db.Memberships.AnyAsync(
            member => member.GroupId == groupId
                && member.RemovedAt == null
                && member.Id != memberId
                && member.DisplayNameKey == key,
            cancellationToken);
        if (collision)
            return CommandOutcome.Refused();

        membership.DisplayName = shown;
        membership.DisplayNameKey = key;
        await _db.SaveChangesAsync(cancellationToken);
        _log.Command("rename-member", actorId, groupId);
        return CommandOutcome.Ok();
    }

    public async Task<CommandOutcome> RemoveAsync(
        GroupId groupId,
        AccountId actorId,
        MemberId memberId,
        CancellationToken cancellationToken = default)
    {
        var group = await OwnedGroup(groupId, actorId, cancellationToken);
        if (group is null)
            return CommandOutcome.NotFound();

        var membership = await _db.Memberships.FirstOrDefaultAsync(
            member => member.Id == memberId && member.GroupId == groupId && member.RemovedAt == null,
            cancellationToken);
        if (membership is null)
            return CommandOutcome.NotFound();
        if (membership.AccountId == group.CreatorAccountId)
            return CommandOutcome.Refused();

        membership.RemovedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        _log.Command("remove-member", actorId, groupId);
        return CommandOutcome.Ok();
    }

    private async Task<Group?> OwnedGroup(GroupId groupId, AccountId actorId, CancellationToken cancellationToken)
    {
        var group = await _db.Groups.FirstOrDefaultAsync(row => row.Id == groupId, cancellationToken);
        if (group is null || group.CreatorAccountId != actorId)
            return null;
        var belongs = await _db.Memberships.AnyAsync(
            member => member.GroupId == groupId && member.AccountId == actorId && member.RemovedAt == null,
            cancellationToken);
        return belongs ? group : null;
    }

    private Task<List<Membership>> Active(GroupId groupId, CancellationToken cancellationToken) =>
        _db.Memberships.Where(member => member.GroupId == groupId && member.RemovedAt == null).ToListAsync(cancellationToken);
}
