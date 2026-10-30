using System.Text;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Quorum.App.Infrastructure.Logging;

public static class TelemetryExtensions
{
    public const string ServiceName = "quorum";
    public const string EndpointKey = "Telemetry:OtlpEndpoint";
    public const string CollectorUserKey = "Telemetry:CollectorUser";
    public const string CollectorPasswordKey = "Telemetry:CollectorPassword";

    public static WebApplicationBuilder AddQuorumTelemetry(this WebApplicationBuilder builder)
    {
        var endpoint = builder.Configuration[EndpointKey];
        var headers = BasicAuthHeader(
            builder.Configuration[CollectorUserKey],
            builder.Configuration[CollectorPasswordKey]);

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = false;
                });
                tracing.AddHttpClientInstrumentation(options =>
                {
                    options.RecordException = false;
                });
                if (!string.IsNullOrWhiteSpace(endpoint))
                    tracing.AddOtlpExporter(options => Configure(options, endpoint, headers));
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation();
                metrics.AddHttpClientInstrumentation();
                if (!string.IsNullOrWhiteSpace(endpoint))
                    metrics.AddOtlpExporter(options => Configure(options, endpoint, headers));
            });

        builder.Logging.AddOpenTelemetry(options =>
        {
            options.IncludeFormattedMessage = false;
            options.IncludeScopes = false;
            options.ParseStateValues = false;
            if (!string.IsNullOrWhiteSpace(endpoint))
                options.AddOtlpExporter(exporter => Configure(exporter, endpoint, headers));
        });

        return builder;
    }

    public static string? BasicAuthHeader(string? user, string? password)
    {
        if (string.IsNullOrEmpty(user) || password is null)
            return null;
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}"));
        return $"Authorization=Basic {token}";
    }

    private static void Configure(OtlpExporterOptions options, string endpoint, string? headers)
    {
        options.Endpoint = new Uri(endpoint);
        options.Protocol = OtlpExportProtocol.HttpProtobuf;
        if (!string.IsNullOrEmpty(headers))
            options.Headers = headers;
    }
}
