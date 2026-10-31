using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Quorum.App.Infrastructure.Auth;
using Quorum.App.Infrastructure.Persistence;

namespace Quorum.App.Features.Accounts;

[AllowAnonymous]
public sealed class RegisterModel : PageModel
{
    private readonly AccountCommands _accounts;
    private readonly QuorumDbContext _db;

    public RegisterModel(AccountCommands accounts, QuorumDbContext db)
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
        var result = await _accounts.RegisterAsync(Email, Password, cancellationToken);
        if (result.Status == AccountCommandStatus.DuplicateEmail)
        {
            Error = AccountCommands.DuplicateEmailMessage;
            return Page();
        }

        if (result.Status != AccountCommandStatus.Created || result.Account is null)
        {
            Error = AccountCommands.InvalidRegistrationMessage;
            return Page();
        }

        var pendingInvitation = await _db.Invitations.AnyAsync(
            invitation => invitation.EmailKey == result.Account.Email
                && invitation.AcceptedAt == null
                && invitation.DeclinedAt == null
                && invitation.CancelledAt == null,
            cancellationToken);
        await SignInSession.SignInAsync(HttpContext, result.Account.Id, null);
        return Redirect(pendingInvitation ? "/groups" : "/groups/new");
    }
}
