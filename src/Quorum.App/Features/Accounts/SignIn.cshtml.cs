using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Quorum.App.Infrastructure.Auth;
using Quorum.App.Infrastructure.Persistence;

namespace Quorum.App.Features.Accounts;

[AllowAnonymous]
public sealed class SignInModel : PageModel
{
    private readonly AccountCommands _accounts;
    private readonly QuorumDbContext _db;

    public SignInModel(AccountCommands accounts, QuorumDbContext db)
    {
        _accounts = accounts;
        _db = db;
    }

    [BindProperty]
    public string Email { get; set; } = "";

    [BindProperty]
    public string Password { get; set; } = "";

    public string? Error { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var result = await _accounts.SignInAsync(Email, Password, cancellationToken);
        if (result.Status != AccountCommandStatus.SignedIn || result.Account is null)
        {
            Error = AccountCommands.SignInFailureMessage;
            return Page();
        }

        var memberships = await _db.Memberships
            .Where(member => member.AccountId == result.Account.Id && member.RemovedAt == null)
            .ToListAsync(cancellationToken);
        var pendingInvitation = await _db.Invitations.AnyAsync(
            invitation => invitation.EmailKey == result.Account.Email
                && invitation.AcceptedAt == null
                && invitation.DeclinedAt == null
                && invitation.CancelledAt == null,
            cancellationToken);
        if (pendingInvitation)
        {
            await SignInSession.SignInAsync(HttpContext, result.Account.Id, null);
            return Redirect("/groups");
        }

        if (memberships.Count == 0)
        {
            await SignInSession.SignInAsync(HttpContext, result.Account.Id, null);
            return Redirect("/groups/new");
        }

        if (memberships.Count == 1)
        {
            await SignInSession.SignInAsync(HttpContext, result.Account.Id, memberships[0].GroupId);
            return Redirect("/catalog");
        }

        await SignInSession.SignInAsync(HttpContext, result.Account.Id, null);
        return Redirect("/groups");
    }
}
