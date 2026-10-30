using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Quorum.App.Infrastructure.Logging;

namespace Quorum.App.Features.Accounts;

[AllowAnonymous]
public sealed class SignOutModel : PageModel
{
    private readonly SafeLog _log;

    public SignOutModel(SafeLog log)
    {
        _log = log;
    }

    public IActionResult OnGet() => Redirect("/account/sign-in");

    public async Task<IActionResult> OnPostAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var account = Infrastructure.Auth.SignInSession.Account(User);
            _log.Begin();
            await _log.RecordAsync(new ActivityWrite("sign-out", true, account?.Value));
            _log.Finish();
            _log.Command("sign-out", account);
        }
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/account/sign-in");
    }
}
