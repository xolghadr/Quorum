namespace Quorum.App.Features.Catalog.Publish;

public static class PublishExtensions
{
    public static WebApplicationBuilder AddCatalogPublish(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<RoadmapParser>();
        builder.Services.AddScoped<PublishCatalog>();
        return builder;
    }

    public static void RunCatalogCommandIfPresent(this WebApplication app)
    {
        var args = Environment.GetCommandLineArgs();
        if (Array.IndexOf(args, "publish-catalog") < 0)
            return;

        var code = PublishCatalog.ExecuteFromCommandLine(app.Services);
        Environment.Exit(code);
    }
}
