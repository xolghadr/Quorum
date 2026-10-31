using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quorum.App.Features.Shared;
using Quorum.App.Infrastructure.Logging;
using Quorum.App.Infrastructure.Persistence;
using Quorum.App.SharedKernel;

namespace Quorum.App.Features.Groups;

public sealed class MembersModel : SignedInPageModel
{
    private readonly QuorumDbContext _db;
    private readonly InvitationCommands _invitations;
    private readonly SafeLog _log;

    public MembersModel(QuorumDbContext db, InvitationCommands invitations, SafeLog log)
    {
        _db = db;
        _invitations = invitations;
        _log = log;
    }

    public bool IsCreator { get; private set; }
    public IReadOnlyList<MemberRow> Members { get; private set; } = [];
    public IReadOnlyList<PendingRow> Pending { get; private set; } = [];
    public string? Error { get; set; }

    [BindProperty]
    public string Email { get; set; } = "";

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var gate = await RequireGroupAsync(_db, cancellationToken);
        if (gate is not null)
            return gate;
        await LoadAsync(cancellationToken);
        if (Request.Query.ContainsKey("error"))
        {
            _log.AdoptReference(Request.Query["reference"]);
            Error = Request.Query["error"] == "invite"
                ? InvitationCommands.NotAcceptedMessage
                : RosterCommands.InvalidNameMessage;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var gate = await RequireGroupAsync(_db, cancellationToken);
        if (gate is not null)
            return gate;

        var result = await _invitations.InviteAsync(OpenGroupId!.Value, AccountId, Email, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.Remove(nameof(Email));
            Email = "";
            Error = InvitationCommands.NotAcceptedMessage;
            await LoadAsync(cancellationToken);
            return Page();
        }

        return Redirect("/members");
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var group = await _db.Groups.FirstAsync(row => row.Id == OpenGroupId, cancellationToken);
        IsCreator = group.CreatorAccountId == AccountId;
        Members = await _db.Memberships
            .Where(member => member.GroupId == OpenGroupId && member.AccountId != null && member.RemovedAt == null)
            .OrderBy(member => member.CreatedAt)
            .Select(member => new MemberRow(
                member.Id.Value,
                member.DisplayName,
                member.AccountId == group.CreatorAccountId,
                member.AccountId == AccountId))
            .ToListAsync(cancellationToken);

        var pending = await _db.Invitations
            .Where(invitation => invitation.GroupId == OpenGroupId
                && invitation.AcceptedAt == null
                && invitation.DeclinedAt == null
                && invitation.CancelledAt == null
                && (IsCreator || invitation.InvitedByAccountId == AccountId))
            .OrderBy(invitation => invitation.CreatedAt)
            .Select(invitation => new PendingRow(invitation.Id.Value, invitation.EmailKey))
            .ToListAsync(cancellationToken);
        Pending = pending;
    }

    public sealed record MemberRow(Guid Id, string DisplayName, bool IsCreator, bool IsSelf);
    public sealed record PendingRow(Guid Id, string Email);
}
