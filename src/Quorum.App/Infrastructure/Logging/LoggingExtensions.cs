namespace Quorum.App.Infrastructure.Logging;

public static class LoggingExtensions
{
    public static WebApplicationBuilder AddQuorumLogging(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<SafeLog>();
        return builder;
    }
}
