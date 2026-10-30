using Quorum.App.Features.Accounts;
using Quorum.App.Features.Groups;
using Quorum.App.Features.Sessions;

namespace Quorum.App.Infrastructure.Web;

public static class WebExtensions
{
    public static WebApplicationBuilder AddQuorumWeb(this WebApplicationBuilder builder)
    {
        builder.Services.AddRazorPages(options =>
        {
            options.RootDirectory = "/Features";
        });
        builder.Services.AddScoped<AccountCommands>();
        builder.Services.AddScoped<GroupCommands>();
        builder.Services.AddScoped<RosterCommands>();
        builder.Services.AddScoped<SessionCommands>();
        builder.Services.AddScoped<Election>();
        builder.Services.AddSingleton<IFacilitatorTieBreak, EqualChanceTieBreak>();
        return builder;
    }

    public static WebApplication UseQuorumWeb(this WebApplication app)
    {
        app.UseStaticFiles();
        app.MapRazorPages();
        app.MapGet("/", (HttpContext http) =>
            Results.Redirect(http.User.Identity?.IsAuthenticated == true
                ? "/groups"
                : "/account/sign-in"));
        return app;
    }
}
