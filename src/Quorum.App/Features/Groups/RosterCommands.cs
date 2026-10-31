using Microsoft.EntityFrameworkCore;
using Quorum.App.Features.Sessions;
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
    private readonly Election _election;

    public RosterCommands(QuorumDbContext db, IClock clock, SafeLog log, Election election)
    {
        _db = db;
        _clock = clock;
        _log = log;
        _election = election;
    }

    public async Task<CommandOutcome> AddAsync(
        GroupId groupId,
        AccountId actorId,
        string? displayName,
        CancellationToken cancellationToken = default)
    {
        _log.Begin();
        _ = displayName;
        var group = await OwnedGroup(groupId, actorId, cancellationToken);
        if (group is null)
            return await Refuse("add-member", actorId, groupId, null, CommandOutcome.NotFound(), cancellationToken);
        return await Refuse("add-member", actorId, groupId, null, CommandOutcome.Refused(), cancellationToken);
    }

    public async Task<CommandOutcome> RenameAsync(
        GroupId groupId,
        AccountId actorId,
        MemberId memberId,
        string? displayName,
        CancellationToken cancellationToken = default)
    {
        _log.Begin();
        var group = await OwnedGroup(groupId, actorId, cancellationToken);
        if (group is null)
            return await Refuse("rename-member", actorId, groupId, memberId, CommandOutcome.NotFound(), cancellationToken);
        if (!Membership.DisplayNameAccepted(displayName, out var shown))
            return await Refuse("rename-member", actorId, groupId, memberId, CommandOutcome.Refused(), cancellationToken);

        var membership = await _db.Memberships.FirstOrDefaultAsync(
            member => member.Id == memberId && member.GroupId == groupId && member.RemovedAt == null,
            cancellationToken);
        if (membership is null)
            return await Refuse("rename-member", actorId, groupId, memberId, CommandOutcome.NotFound(), cancellationToken);
        if (membership.AccountId is null)
            return await Refuse("rename-member", actorId, groupId, memberId, CommandOutcome.Refused(), cancellationToken);

        var key = Membership.KeyFor(shown);
        var collision = await _db.Memberships.AnyAsync(
            member => member.GroupId == groupId
                && member.RemovedAt == null
                && member.Id != memberId
                && member.DisplayNameKey == key,
            cancellationToken);
        if (collision)
            return await Refuse("rename-member", actorId, groupId, memberId, CommandOutcome.Refused(), cancellationToken);

        membership.DisplayName = shown;
        membership.DisplayNameKey = key;
        await _db.SaveChangesAsync(cancellationToken);
        await RecordAsync(new ActivityWrite("rename-member", true, actorId.Value, groupId.Value, MemberId: memberId.Value), cancellationToken);
        _log.Command("rename-member", actorId, groupId);
        return CommandOutcome.Ok();
    }

    public async Task<CommandOutcome> RemoveAsync(
        GroupId groupId,
        AccountId actorId,
        MemberId memberId,
        CancellationToken cancellationToken = default)
    {
        _log.Begin();
        var group = await OwnedGroup(groupId, actorId, cancellationToken);
        if (group is null)
            return await Refuse("remove-member", actorId, groupId, memberId, CommandOutcome.NotFound(), cancellationToken);

        var membership = await _db.Memberships.FirstOrDefaultAsync(
            member => member.Id == memberId && member.GroupId == groupId && member.RemovedAt == null,
            cancellationToken);
        if (membership is null)
            return await Refuse("remove-member", actorId, groupId, memberId, CommandOutcome.NotFound(), cancellationToken);
        if (membership.AccountId == group.CreatorAccountId)
            return await Refuse("remove-member", actorId, groupId, memberId, CommandOutcome.Refused(), cancellationToken);

        return await EndMembershipAsync(groupId, actorId, membership, "remove-member", cancellationToken);
    }

    public async Task<CommandOutcome> LeaveAsync(
        GroupId groupId,
        AccountId actorId,
        CancellationToken cancellationToken = default)
    {
        _log.Begin();
        var group = await _db.Groups.FirstOrDefaultAsync(row => row.Id == groupId, cancellationToken);
        var membership = await _db.Memberships.FirstOrDefaultAsync(
            member => member.GroupId == groupId && member.AccountId == actorId && member.RemovedAt == null,
            cancellationToken);
        if (group is null || membership is null)
            return await Refuse("leave-group", actorId, groupId, membership?.Id, CommandOutcome.NotFound(), cancellationToken);
        if (group.CreatorAccountId == actorId)
            return await Refuse("leave-group", actorId, groupId, membership.Id, CommandOutcome.Refused(), cancellationToken);

        return await EndMembershipAsync(groupId, actorId, membership, "leave-group", cancellationToken);
    }

    private async Task<CommandOutcome> EndMembershipAsync(
        GroupId groupId,
        AccountId actorId,
        Membership membership,
        string attempt,
        CancellationToken cancellationToken)
    {
        membership.RemovedAt = _clock.UtcNow;
        await _election.ApplyDepartureAsync(groupId, membership.Id, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await RecordAsync(new ActivityWrite(attempt, true, actorId.Value, groupId.Value, MemberId: membership.Id.Value), cancellationToken);
        _log.Command(attempt, actorId, groupId);
        return CommandOutcome.Ok();
    }

    private async Task<CommandOutcome> Refuse(
        string attempt,
        AccountId actorId,
        GroupId groupId,
        MemberId? memberId,
        CommandOutcome outcome,
        CancellationToken cancellationToken)
    {
        await RecordAsync(new ActivityWrite(attempt, false, actorId.Value, groupId.Value, MemberId: memberId?.Value), cancellationToken);
        return outcome;
    }

    private async Task RecordAsync(ActivityWrite write, CancellationToken cancellationToken)
    {
        await _log.RecordAsync(write, cancellationToken);
        _log.Finish();
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
}
