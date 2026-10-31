using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quorum.App.Features.Shared;
using Quorum.App.Infrastructure.Auth;
using Quorum.App.Infrastructure.Logging;
using Quorum.App.Infrastructure.Persistence;
using Quorum.App.SharedKernel;

namespace Quorum.App.Features.Groups;

public sealed class AcceptInvitationModel : SignedInPageModel
{
    private readonly QuorumDbContext _db;
    private readonly InvitationCommands _invitations;

    public AcceptInvitationModel(QuorumDbContext db, InvitationCommands invitations)
    {
        _db = db;
        _invitations = invitations;
    }

    public string GroupName { get; private set; } = "";
    public string InviterDisplayName { get; private set; } = "";
    public string? Error { get; set; }

    [BindProperty]
    public string DisplayName { get; set; } = "";

    public async Task<IActionResult> OnGetAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        var invitation = await FindOwnAsync(invitationId, cancellationToken);
        if (invitation is null)
            return NotFound();
        await LoadNamesAsync(invitation, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        var invitation = await FindOwnAsync(invitationId, cancellationToken);
        if (invitation is null)
            return NotFound();

        var result = await _invitations.AcceptAsync(AccountId, new InvitationId(invitationId), DisplayName, cancellationToken);
        if (result.Succeeded && result.Id is { } groupId)
        {
            await SignInSession.SignInAsync(HttpContext, AccountId, new GroupId(groupId));
            return Redirect("/catalog");
        }

        if (result.Status == Shared.CommandStatus.NotFound)
            return NotFound();

        await LoadNamesAsync(invitation, cancellationToken);
        var active = await _db.Memberships.CountAsync(
            member => member.GroupId == invitation.GroupId && member.AccountId != null && member.RemovedAt == null,
            cancellationToken);
        var nameOk = Membership.DisplayNameAccepted(DisplayName, out var shown);
        var taken = nameOk && await _db.Memberships.AnyAsync(
            member => member.GroupId == invitation.GroupId && member.RemovedAt == null && member.DisplayNameKey == Membership.KeyFor(shown),
            cancellationToken);
        Error = nameOk && !taken && active >= Group.MemberMax
            ? InvitationCommands.GroupFullMessage
            : InvitationCommands.NameNotAcceptedMessage;
        return Page();
    }

    private async Task<Invitation?> FindOwnAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        var email = await _db.Accounts
            .Where(account => account.Id == AccountId)
            .Select(account => account.Email)
            .SingleAsync(cancellationToken);
        var id = new InvitationId(invitationId);
        return await _db.Invitations.FirstOrDefaultAsync(
            invitation => invitation.Id == id
                && invitation.EmailKey == email
                && invitation.AcceptedAt == null
                && invitation.DeclinedAt == null
                && invitation.CancelledAt == null,
            cancellationToken);
    }

    private async Task LoadNamesAsync(Invitation invitation, CancellationToken cancellationToken)
    {
        GroupName = await _db.Groups
            .Where(group => group.Id == invitation.GroupId)
            .Select(group => group.Name)
            .SingleAsync(cancellationToken);
        InviterDisplayName = await _db.Memberships
            .Where(member => member.GroupId == invitation.GroupId && member.AccountId == invitation.InvitedByAccountId)
            .OrderBy(member => member.RemovedAt == null ? 0 : 1)
            .Select(member => member.DisplayName)
            .FirstAsync(cancellationToken);
    }
}

public sealed class DeclineInvitationModel : SignedInPageModel
{
    private readonly InvitationCommands _invitations;
    private readonly SafeLog _log;

    public DeclineInvitationModel(InvitationCommands invitations, SafeLog log)
    {
        _invitations = invitations;
        _log = log;
    }

    public async Task<IActionResult> OnPostAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        var result = await _invitations.DeclineAsync(AccountId, new InvitationId(invitationId), cancellationToken);
        if (result.Status == Shared.CommandStatus.NotFound)
            return NotFound();
        return Redirect(result.Succeeded ? "/groups" : "/groups?error=1" + _log.ReferenceParameter());
    }
}

public sealed class CancelInvitationModel : SignedInPageModel
{
    private readonly InvitationCommands _invitations;
    private readonly SafeLog _log;

    public CancelInvitationModel(InvitationCommands invitations, SafeLog log)
    {
        _invitations = invitations;
        _log = log;
    }

    public async Task<IActionResult> OnPostAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        if (OpenGroupId is not { } groupId)
            return Challenge();
        var result = await _invitations.CancelAsync(groupId, AccountId, new InvitationId(invitationId), cancellationToken);
        if (result.Status == Shared.CommandStatus.NotFound)
            return NotFound();
        return Redirect(result.Succeeded ? "/members" : "/members?error=invite" + _log.ReferenceParameter());
    }
}
