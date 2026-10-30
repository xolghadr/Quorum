using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quorum.App.Infrastructure.Logging;

namespace Quorum.App.Tests;

public sealed class ActivityExportShapeTests
{
    [Fact]
    public async Task Attempts_add_up_and_times_follow_acceptance()
    {
        var succeededAt = new[]
        {
            new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 3, 1, 12, 10, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 3, 1, 12, 20, 0, TimeSpan.Zero),
        };
        var refusedAt = new DateTimeOffset(2026, 3, 1, 12, 30, 0, TimeSpan.Zero);
        var rows = succeededAt
            .Select(instant => Row("sign-in", "succeeded", instant))
            .Append(Row("sign-in", "refused", refusedAt))
            .ToList();
        var handler = new CaptureHandler();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Telemetry:CollectorUser"] = "quorum",
            ["Telemetry:CollectorPassword"] = "secret",
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddHttpClient("quorum-otlp").ConfigurePrimaryHttpMessageHandler(() => handler);
        await using var provider = services.BuildServiceProvider();
        var sender = new OtlpActivityExport(provider.GetRequiredService<IHttpClientFactory>(), configuration);
        var batch = new ActivityExportBatch
        {
            Endpoint = "http://127.0.0.1:4318",
            SpanName = ActivityExportBatch.Span,
            Reference = "23456789",
            ResourceAttributes = new Dictionary<string, string> { ["service.name"] = "quorum" },
            Rows = rows,
        };

        Assert.True(await sender.SendAsync(batch, CancellationToken.None));

        using var metrics = JsonDocument.Parse(handler.BodyFor("/v1/metrics"));
        var sum = metrics.RootElement
            .GetProperty("resourceMetrics")[0]
            .GetProperty("scopeMetrics")[0]
            .GetProperty("metrics")
            .EnumerateArray()
            .Single(metric => metric.GetProperty("name").GetString() == ActivityExportBatch.Attempts)
            .GetProperty("sum");
        Assert.Equal(1, sum.GetProperty("aggregationTemporality").GetInt32());
        var points = sum.GetProperty("dataPoints").EnumerateArray().ToList();
        Assert.Equal(2, points.Count);
        Assert.Equal("3", Point(points, "succeeded").GetProperty("asInt").GetString());
        Assert.Equal(UnixNano(succeededAt[2]), Point(points, "succeeded").GetProperty("timeUnixNano").GetString());
        Assert.Equal("1", Point(points, "refused").GetProperty("asInt").GetString());
        Assert.Equal(UnixNano(refusedAt), Point(points, "refused").GetProperty("timeUnixNano").GetString());

        using var logs = JsonDocument.Parse(handler.BodyFor("/v1/logs"));
        var records = logs.RootElement
            .GetProperty("resourceLogs")[0]
            .GetProperty("scopeLogs")[0]
            .GetProperty("logRecords")
            .EnumerateArray()
            .Select(record => record.GetProperty("timeUnixNano").GetString())
            .ToList();
        Assert.Equal(
            succeededAt.Append(refusedAt).Select(UnixNano).ToArray(),
            records.Select(value => value ?? "").ToArray());
        Assert.DoesNotContain(handler.BodyFor("/v1/logs"), "quorum.group_id=", StringComparison.Ordinal);

        using var traces = JsonDocument.Parse(handler.BodyFor("/v1/traces"));
        var span = traces.RootElement
            .GetProperty("resourceSpans")[0]
            .GetProperty("scopeSpans")[0]
            .GetProperty("spans")[0];
        Assert.Equal(UnixNano(succeededAt[0] - TimeSpan.FromMilliseconds(5)), span.GetProperty("startTimeUnixNano").GetString());
        Assert.Equal(UnixNano(refusedAt), span.GetProperty("endTimeUnixNano").GetString());
    }

    [Fact]
    public void Dashboard_filters_attributes_without_mixing_outcomes()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "docker-compose.yml")))
            root = root.Parent;
        Assert.NotNull(root);
        var json = File.ReadAllText(Path.Combine(root.FullName, "deploy/grafana/provisioning/dashboards/quorum.json"));
        using var dashboard = JsonDocument.Parse(json);
        var logs = dashboard.RootElement.GetProperty("panels").EnumerateArray()
            .Single(panel => panel.GetProperty("title").GetString() == "Activity");
        var expr = logs.GetProperty("targets")[0].GetProperty("expr").GetString();
        Assert.NotNull(expr);
        Assert.Contains("quorum_reference", expr, StringComparison.Ordinal);
        Assert.Contains("quorum_outcome", expr, StringComparison.Ordinal);
        Assert.Contains("quorum_group_id", expr, StringComparison.Ordinal);
        Assert.Contains("quorum_session_id", expr, StringComparison.Ordinal);
        Assert.Contains("eq \"${outcome}\" \"\"", expr, StringComparison.Ordinal);
        Assert.Contains("eq .quorum_outcome \"${outcome}\"", expr, StringComparison.Ordinal);
        Assert.Contains("eq \"${group_id}\" \"\"", expr, StringComparison.Ordinal);
        Assert.Contains("eq \"${session_id}\" \"\"", expr, StringComparison.Ordinal);
        var outcome = dashboard.RootElement.GetProperty("templating").GetProperty("list").EnumerateArray()
            .Single(variable => variable.GetProperty("name").GetString() == "outcome");
        Assert.Equal("refused,succeeded", outcome.GetProperty("query").GetString());
        Assert.Equal("", outcome.GetProperty("allValue").GetString());
        Assert.DoesNotContain("outcome=refused|outcome=succeeded", json, StringComparison.Ordinal);
        Assert.Contains("\"from\": \"now-1h\"", json, StringComparison.Ordinal);
        var trace = dashboard.RootElement.GetProperty("panels").EnumerateArray()
            .Single(panel => panel.GetProperty("title").GetString() == "Attempt");
        var traceQuery = trace.GetProperty("targets")[0].GetProperty("query").GetString();
        Assert.NotNull(traceQuery);
        Assert.Contains("name = \"quorum.attempt\"", traceQuery, StringComparison.Ordinal);
        Assert.Contains("span.quorum.reference = \"$reference\"", traceQuery, StringComparison.Ordinal);
        Assert.Contains("span.quorum.storage_wait_ms =~ \".+\"", traceQuery, StringComparison.Ordinal);
        Assert.DoesNotContain(">= 0", traceQuery, StringComparison.Ordinal);
    }

    [Fact]
    public void Hourly_panels_count_each_attempt_sample()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "docker-compose.yml")))
            root = root.Parent;
        Assert.NotNull(root);
        var json = File.ReadAllText(Path.Combine(root.FullName, "deploy/grafana/provisioning/dashboards/quorum.json"));
        using var dashboard = JsonDocument.Parse(json);
        var succeeded = dashboard.RootElement.GetProperty("panels").EnumerateArray()
            .Single(panel => panel.GetProperty("title").GetString() == "Succeeded");
        var refused = dashboard.RootElement.GetProperty("panels").EnumerateArray()
            .Single(panel => panel.GetProperty("title").GetString() == "Refused");
        Assert.Equal(
            "sum(count_over_time(quorum_attempts_total{outcome=\"succeeded\"}[1h]))",
            succeeded.GetProperty("targets")[0].GetProperty("expr").GetString());
        Assert.Equal(
            "sum(count_over_time(quorum_attempts_total{outcome=\"refused\"}[1h]))",
            refused.GetProperty("targets")[0].GetProperty("expr").GetString());
        Assert.True(succeeded.GetProperty("targets")[0].GetProperty("instant").GetBoolean());
        Assert.True(refused.GetProperty("targets")[0].GetProperty("instant").GetBoolean());
        Assert.Equal(0, succeeded.GetProperty("fieldConfig").GetProperty("defaults").GetProperty("decimals").GetInt32());
        Assert.Equal(0, refused.GetProperty("fieldConfig").GetProperty("defaults").GetProperty("decimals").GetInt32());
        Assert.DoesNotContain("increase(", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Collector_accumulates_attempt_deltas_before_prometheus()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "docker-compose.yml")))
            root = root.Parent;
        Assert.NotNull(root);
        var yaml = File.ReadAllText(Path.Combine(root.FullName, "deploy/otel-collector.yaml"));
        Assert.Contains("delta_to_cumulative:", yaml, StringComparison.Ordinal);
        Assert.Contains("max_stale: 336h", yaml, StringComparison.Ordinal);
        Assert.Contains(
            "processors: [attributes/allowlist, transform/allowlist, delta_to_cumulative]",
            yaml,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "processors: [attributes/allowlist, transform/allowlist]\n      exporters: [prometheusremotewrite]",
            yaml,
            StringComparison.Ordinal);
    }

    private static ActivityExportRow Row(string attempt, string outcome, DateTimeOffset acceptedAt) => new()
    {
        Attempt = attempt,
        Outcome = outcome,
        DurationMs = 5,
        StorageWaitMs = 1,
        AcceptedAt = acceptedAt,
        Attributes = new Dictionary<string, string>
        {
            ["quorum.attempt"] = attempt,
            ["quorum.outcome"] = outcome,
            ["quorum.reference"] = "23456789",
            ["quorum.duration_ms"] = "5",
            ["quorum.storage_wait_ms"] = "1",
            ["quorum.group_id"] = "",
            ["quorum.session_id"] = "",
        },
    };

    private static JsonElement Point(List<JsonElement> points, string outcome) =>
        points.Single(point => point.GetProperty("attributes")
            .EnumerateArray()
            .Any(attribute => attribute.GetProperty("key").GetString() == "outcome"
                && attribute.GetProperty("value").GetProperty("stringValue").GetString() == outcome));

    private static string UnixNano(DateTimeOffset instant) =>
        ((instant.UtcDateTime - DateTime.UnixEpoch).Ticks * 100L).ToString(CultureInfo.InvariantCulture);

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public List<Uri> Uris { get; } = [];
        public List<string> Bodies { get; } = [];

        public string BodyFor(string path) =>
            Bodies[Uris.FindIndex(uri => uri.AbsolutePath == path)];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uris.Add(request.RequestUri!);
            Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
