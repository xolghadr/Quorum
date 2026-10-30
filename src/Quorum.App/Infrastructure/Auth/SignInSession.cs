using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Quorum.App.SharedKernel;

namespace Quorum.App.Infrastructure.Auth;

public static class SignInSession
{
    public static async Task SignInAsync(HttpContext http, AccountId accountId, GroupId? groupId)
    {
        var claims = new List<Claim>
        {
            new(AuthExtensions.AccountClaim, accountId.Value.ToString()),
        };
        if (groupId is { } openGroup)
            claims.Add(new Claim(AuthExtensions.GroupClaim, openGroup.Value.ToString()));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await http.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = true,
                AllowRefresh = true,
            });
    }

    public static AccountId? Account(ClaimsPrincipal user)
    {
        var value = user.FindFirst(AuthExtensions.AccountClaim)?.Value;
        return Guid.TryParse(value, out var id) ? new AccountId(id) : null;
    }

    public static GroupId? OpenGroup(ClaimsPrincipal user)
    {
        var value = user.FindFirst(AuthExtensions.GroupClaim)?.Value;
        return Guid.TryParse(value, out var id) ? new GroupId(id) : null;
    }
}
