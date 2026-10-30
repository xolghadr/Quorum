namespace Quorum.App.Infrastructure.Logging;

public static class LoggingExtensions
{
    public static WebApplicationBuilder AddQuorumLogging(this WebApplicationBuilder builder)
    {
        builder.Services.AddHttpClient("quorum-otlp");
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<SafeLog>();
        builder.Services.AddSingleton<HealthMonitor>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<HealthMonitor>());
        builder.Services.AddSingleton<IActivityExport, OtlpActivityExport>();
        builder.Services.AddSingleton<ActivityExporter>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<ActivityExporter>());
        builder.Services.AddSingleton<ActivityRetention>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<ActivityRetention>());
        return builder;
    }
}
