using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Quorum.App.Features.Accounts;

namespace Quorum.App.Infrastructure.Auth;

public static class AuthExtensions
{
    public const string AccountClaim = "account_id";
    public const string GroupClaim = "open_group";
    public const string CookieName = "quorum.auth";

    public static WebApplicationBuilder AddQuorumAuth(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = CookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan = TimeSpan.FromDays(14);
                options.SlidingExpiration = true;
                options.LoginPath = "/account/sign-in";
                options.AccessDeniedPath = "/account/sign-in";
            });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IPasswordHasher<Account>, PasswordHasher<Account>>();
        return builder;
    }

    public static WebApplication UseQuorumAuth(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }
}
