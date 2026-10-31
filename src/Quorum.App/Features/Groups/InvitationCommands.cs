using Microsoft.EntityFrameworkCore;
using Quorum.App.Features.Accounts;
using Quorum.App.Features.Shared;
using Quorum.App.Infrastructure.Logging;
using Quorum.App.Infrastructure.Persistence;
using Quorum.App.SharedKernel;

namespace Quorum.App.Features.Groups;

public sealed class InvitationCommands
{
    public const string NotAcceptedMessage = "That invitation was not saved.";
    public const string NameNotAcceptedMessage = "The name was not accepted.";
    public const string GroupFullMessage = "The group is full.";

    private readonly QuorumDbContext _db;
    private readonly IClock _clock;
    private readonly SafeLog _log;

    public InvitationCommands(QuorumDbContext db, IClock clock, SafeLog log)
    {
        _db = db;
        _clock = clock;
        _log = log;
    }

    public async Task<CommandOutcome> InviteAsync(
        GroupId groupId,
        AccountId actorId,
        string? email,
        CancellationToken cancellationToken = default)
    {
        _log.Begin();
        if (await AcceptedMember(groupId, actorId, cancellationToken) is null)
            return await Refuse("invite-member", actorId, groupId, CommandOutcome.NotFound(), cancellationToken);
        if (!Account.EmailAccepted(email))
            return await Refuse("invite-member", actorId, groupId, CommandOutcome.Refused(), cancellationToken);

        var key = Account.NormalizeEmail(email!);
        var actorEmail = await _db.Accounts
            .Where(account => account.Id == actorId)
            .Select(account => account.Email)
            .SingleAsync(cancellationToken);
        if (actorEmail == key)
            return await Refuse("invite-member", actorId, groupId, CommandOutcome.Refused(), cancellationToken);
        if (await EmailIsActiveMember(groupId, key, cancellationToken))
            return await Refuse("invite-member", actorId, groupId, CommandOutcome.Refused(), cancellationToken);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await LockGroupAsync(groupId, cancellationToken);
        if (await Pending(groupId).AnyAsync(invitation => invitation.EmailKey == key, cancellationToken))
            return await Refuse("invite-member", actorId, groupId, CommandOutcome.Refused(), cancellationToken);

        var active = await ActiveAccountCount(groupId, cancellationToken);
        var pending = await Pending(groupId).CountAsync(cancellationToken);
        if (!GroupCap.Fits(active, pending + 1))
            return await Refuse("invite-member", actorId, groupId, CommandOutcome.Refused(), cancellationToken);

        var invitation = new Invitation
        {
            Id = InvitationId.New(),
            GroupId = groupId,
            EmailKey = key,
            InvitedByAccountId = actorId,
            CreatedAt = _clock.UtcNow,
        };
        _db.Invitations.Add(invitation);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            return await Refuse("invite-member", actorId, groupId, CommandOutcome.Refused(), cancellationToken);
        }

        await RecordAsync(new ActivityWrite("invite-member", true, actorId.Value, groupId.Value), cancellationToken);
        _log.Command("invite-member", actorId, groupId);
        return CommandOutcome.Ok(invitation.Id.Value);
    }

    public async Task<CommandOutcome> CancelAsync(
        GroupId groupId,
        AccountId actorId,
        InvitationId invitationId,
        CancellationToken cancellationToken = default)
    {
        _log.Begin();
        if (await AcceptedMember(groupId, actorId, cancellationToken) is null)
            return await Refuse("cancel-invitation", actorId, groupId, CommandOutcome.NotFound(), cancellationToken);

        var invitation = await Pending(groupId).FirstOrDefaultAsync(row => row.Id == invitationId, cancellationToken);
        if (invitation is null)
            return await Refuse("cancel-invitation", actorId, groupId, CommandOutcome.NotFound(), cancellationToken);

        var creatorId = await _db.Groups
            .Where(group => group.Id == groupId)
            .Select(group => group.CreatorAccountId)
            .SingleAsync(cancellationToken);
        if (invitation.InvitedByAccountId != actorId && creatorId != actorId)
            return await Refuse("cancel-invitation", actorId, groupId, CommandOutcome.Refused(), cancellationToken);

        invitation.CancelledAt = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await RecordAsync(new ActivityWrite("cancel-invitation", true, actorId.Value, groupId.Value), cancellationToken);
        _log.Command("cancel-invitation", actorId, groupId);
        return CommandOutcome.Ok();
    }

    public async Task<CommandOutcome> DeclineAsync(
        AccountId actorId,
        InvitationId invitationId,
        CancellationToken cancellationToken = default)
    {
        _log.Begin();
        var email = await AccountEmail(actorId, cancellationToken);
        if (email is null)
            return await Refuse("decline-invitation", actorId, groupId: null, CommandOutcome.NotFound(), cancellationToken);

        var invitation = await _db.Invitations.FirstOrDefaultAsync(
            row => row.Id == invitationId
                && row.EmailKey == email
                && row.AcceptedAt == null
                && row.DeclinedAt == null
                && row.CancelledAt == null,
            cancellationToken);
        if (invitation is null)
            return await Refuse("decline-invitation", actorId, groupId: null, CommandOutcome.NotFound(), cancellationToken);

        invitation.DeclinedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await RecordAsync(new ActivityWrite("decline-invitation", true, actorId.Value, invitation.GroupId.Value), cancellationToken);
        _log.Command("decline-invitation", actorId, invitation.GroupId);
        return CommandOutcome.Ok();
    }

    public async Task<CommandOutcome> AcceptAsync(
        AccountId actorId,
        InvitationId invitationId,
        string? displayName,
        CancellationToken cancellationToken = default)
    {
        _log.Begin();
        var email = await AccountEmail(actorId, cancellationToken);
        if (email is null)
            return await Refuse("accept-invitation", actorId, groupId: null, CommandOutcome.NotFound(), cancellationToken);
        if (!Membership.DisplayNameAccepted(displayName, out var shown))
            return await Refuse("accept-invitation", actorId, groupId: null, CommandOutcome.Refused(), cancellationToken);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var invitation = await _db.Invitations.FirstOrDefaultAsync(
            row => row.Id == invitationId
                && row.EmailKey == email
                && row.AcceptedAt == null
                && row.DeclinedAt == null
                && row.CancelledAt == null,
            cancellationToken);
        if (invitation is null)
            return await Refuse("accept-invitation", actorId, groupId: null, CommandOutcome.NotFound(), cancellationToken);

        await LockGroupAsync(invitation.GroupId, cancellationToken);
        var nameKey = Membership.KeyFor(shown);
        var nameTaken = await _db.Memberships.AnyAsync(
            member => member.GroupId == invitation.GroupId && member.RemovedAt == null && member.DisplayNameKey == nameKey,
            cancellationToken);
        if (nameTaken)
            return await Refuse("accept-invitation", actorId, invitation.GroupId, CommandOutcome.Refused(), cancellationToken);

        var active = await ActiveAccountCount(invitation.GroupId, cancellationToken);
        if (active >= Group.MemberMax)
            return await Refuse("accept-invitation", actorId, invitation.GroupId, CommandOutcome.Refused(), cancellationToken);

        invitation.AcceptedAt = _clock.UtcNow;
        var membership = new Membership
        {
            Id = MemberId.New(),
            GroupId = invitation.GroupId,
            AccountId = actorId,
            DisplayName = shown,
            DisplayNameKey = nameKey,
            CreatedAt = _clock.UtcNow,
        };
        _db.Memberships.Add(membership);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            return await Refuse("accept-invitation", actorId, invitation.GroupId, CommandOutcome.Refused(), cancellationToken);
        }

        await RecordAsync(
            new ActivityWrite("accept-invitation", true, actorId.Value, invitation.GroupId.Value, MemberId: membership.Id.Value),
            cancellationToken);
        _log.Command("accept-invitation", actorId, invitation.GroupId);
        return CommandOutcome.Ok(invitation.GroupId.Value);
    }

    private async Task<CommandOutcome> Refuse(
        string attempt,
        AccountId actorId,
        GroupId? groupId,
        CommandOutcome outcome,
        CancellationToken cancellationToken)
    {
        await RecordAsync(new ActivityWrite(attempt, false, actorId.Value, groupId?.Value), cancellationToken);
        return outcome;
    }

    private async Task RecordAsync(ActivityWrite write, CancellationToken cancellationToken)
    {
        await _log.RecordAsync(write, cancellationToken);
        _log.Finish();
    }

    private async Task LockGroupAsync(GroupId groupId, CancellationToken cancellationToken)
    {
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({groupId.Value.ToString()})::bigint)",
            cancellationToken);
    }

    private Task<Membership?> AcceptedMember(GroupId groupId, AccountId actorId, CancellationToken cancellationToken) =>
        _db.Memberships.FirstOrDefaultAsync(
            member => member.GroupId == groupId && member.AccountId == actorId && member.RemovedAt == null,
            cancellationToken);

    private Task<int> ActiveAccountCount(GroupId groupId, CancellationToken cancellationToken) =>
        _db.Memberships.CountAsync(
            member => member.GroupId == groupId && member.AccountId != null && member.RemovedAt == null,
            cancellationToken);

    private IQueryable<Invitation> Pending(GroupId groupId) =>
        _db.Invitations.Where(invitation =>
            invitation.GroupId == groupId
            && invitation.AcceptedAt == null
            && invitation.DeclinedAt == null
            && invitation.CancelledAt == null);

    private async Task<bool> EmailIsActiveMember(GroupId groupId, string emailKey, CancellationToken cancellationToken)
    {
        var emails = await (
            from member in _db.Memberships
            join account in _db.Accounts on member.AccountId equals account.Id
            where member.GroupId == groupId && member.RemovedAt == null && member.AccountId != null
            select account.Email).ToListAsync(cancellationToken);
        return emails.Contains(emailKey);
    }

    private async Task<string?> AccountEmail(AccountId actorId, CancellationToken cancellationToken) =>
        await _db.Accounts
            .Where(account => account.Id == actorId)
            .Select(account => account.Email)
            .FirstOrDefaultAsync(cancellationToken);
}
