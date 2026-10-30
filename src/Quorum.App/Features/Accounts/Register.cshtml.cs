using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Quorum.App.Infrastructure.Auth;

namespace Quorum.App.Features.Accounts;

[AllowAnonymous]
public sealed class RegisterModel : PageModel
{
    private readonly AccountCommands _accounts;

    public RegisterModel(AccountCommands accounts)
    {
        _accounts = accounts;
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

        await SignInSession.SignInAsync(HttpContext, result.Account.Id, null);
        return Redirect("/groups/new");
    }
}
