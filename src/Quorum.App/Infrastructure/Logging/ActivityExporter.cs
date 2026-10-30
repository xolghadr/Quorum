using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Quorum.App.Infrastructure.Persistence;
using Quorum.App.SharedKernel;

namespace Quorum.App.Infrastructure.Logging;

public sealed class ActivityExporter : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    private readonly IServiceScopeFactory _scopes;
    private readonly IActivityExport _export;
    private readonly IConfiguration _configuration;
    private readonly IClock _clock;

    public ActivityExporter(
        IServiceScopeFactory scopes,
        IActivityExport export,
        IConfiguration configuration,
        IClock clock)
    {
        _scopes = scopes;
        _export = export;
        _configuration = configuration;
        _clock = clock;
    }

    public async Task ExportOnceAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<QuorumDbContext>();
        var cutoff = _clock.UtcNow - ActivityRetention.Keep;
        var pending = await db.ActivityRecords
            .Where(row => row.ExportedAt == null && row.AcceptedAt >= cutoff)
            .OrderBy(row => row.AcceptedAt)
            .ToListAsync(cancellationToken);
        if (pending.Count == 0)
            return;

        var endpoint = _configuration[TelemetryExtensions.EndpointKey] ?? "";
        foreach (var group in pending.GroupBy(row => row.Reference))
        {
            var rows = group.ToList();
            var batch = new ActivityExportBatch
            {
                Endpoint = endpoint,
                SpanName = ActivityExportBatch.Span,
                Reference = group.Key,
                ResourceAttributes = new Dictionary<string, string>
                {
                    ["service.name"] = TelemetryExtensions.ServiceName,
                },
                Rows = rows.Select(ToRow).ToList(),
            };
            bool accepted;
            try
            {
                accepted = await _export.SendAsync(batch, cancellationToken);
            }
            catch
            {
                accepted = false;
            }

            if (!accepted)
                continue;

            var exportedAt = _clock.UtcNow;
            foreach (var row in rows)
                row.ExportedAt = exportedAt;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExportOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Leave ExportedAt null and try the same rows on a later pass.
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private static ActivityExportRow ToRow(ActivityRecord row)
    {
        var attributes = new Dictionary<string, string>
        {
            ["quorum.attempt"] = row.Attempt,
            ["quorum.outcome"] = row.Outcome,
            ["quorum.reference"] = row.Reference,
            ["quorum.duration_ms"] = row.DurationMs.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["quorum.storage_wait_ms"] = row.StorageWaitMs.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        Add(attributes, "quorum.reason", row.Reason);
        Add(attributes, "quorum.account_id", row.AccountId);
        attributes["quorum.group_id"] = row.GroupId?.ToString() ?? "";
        attributes["quorum.session_id"] = row.SessionId?.ToString() ?? "";
        Add(attributes, "quorum.member_id", row.MemberId);
        Add(attributes, "quorum.tracks", row.Tracks);
        Add(attributes, "quorum.topics", row.Topics);
        Add(attributes, "quorum.designs", row.Designs);
        return new ActivityExportRow
        {
            Attempt = row.Attempt,
            Outcome = row.Outcome,
            DurationMs = row.DurationMs,
            StorageWaitMs = row.StorageWaitMs,
            AcceptedAt = row.AcceptedAt,
            Attributes = attributes,
        };
    }

    private static void Add(Dictionary<string, string> attributes, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            attributes[key] = value;
    }

    private static void Add(Dictionary<string, string> attributes, string key, Guid? value)
    {
        if (value is { } id)
            attributes[key] = id.ToString();
    }

    private static void Add(Dictionary<string, string> attributes, string key, int? value)
    {
        if (value is { } number)
            attributes[key] = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}

public sealed class OtlpActivityExport : IActivityExport
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _configuration;

    public OtlpActivityExport(IHttpClientFactory http, IConfiguration configuration)
    {
        _http = http;
        _configuration = configuration;
    }

    public async Task<bool> SendAsync(ActivityExportBatch batch, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(batch.Endpoint, UriKind.Absolute, out var endpoint))
            return false;
        if (endpoint.Host is "loki" or "prometheus" or "tempo" or "grafana")
            return false;

        var client = _http.CreateClient("quorum-otlp");
        var user = _configuration[TelemetryExtensions.CollectorUserKey] ?? "";
        var password = _configuration[TelemetryExtensions.CollectorPasswordKey] ?? "";
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);

        var traces = await PostAsync(client, new Uri(endpoint, "/v1/traces"), Traces(batch), cancellationToken);
        var metrics = await PostAsync(client, new Uri(endpoint, "/v1/metrics"), Metrics(batch), cancellationToken);
        var logs = await PostAsync(client, new Uri(endpoint, "/v1/logs"), Logs(batch), cancellationToken);
        return traces && metrics && logs;
    }

    private static async Task<bool> PostAsync(HttpClient client, Uri uri, string json, CancellationToken cancellationToken)
    {
        using var body = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(uri, body, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private static string Traces(ActivityExportBatch batch)
    {
        var start = batch.Rows.Min(row => row.AcceptedAt - TimeSpan.FromMilliseconds(row.DurationMs));
        var end = batch.Rows.Max(row => row.AcceptedAt);
        var attributes = batch.Rows.SelectMany(row => row.Attributes)
            .GroupBy(pair => pair.Key)
            .Select(group => group.First())
            .Append(new KeyValuePair<string, string>("quorum.reference", batch.Reference))
            .Where(pair => ActivityExportBatch.Allowlist.Contains(pair.Key));
        return JsonSerializer.Serialize(new
        {
            resourceSpans = new[]
            {
                new
                {
                    resource = new { attributes = Attributes(batch.ResourceAttributes) },
                    scopeSpans = new[]
                    {
                        new
                        {
                            scope = new { name = TelemetryExtensions.ServiceName },
                            spans = new[]
                            {
                                new
                                {
                                    traceId = Guid.NewGuid().ToString("N"),
                                    spanId = Guid.NewGuid().ToString("N")[..16],
                                    name = batch.SpanName,
                                    startTimeUnixNano = UnixNano(start),
                                    endTimeUnixNano = UnixNano(end),
                                    attributes = Attributes(attributes),
                                },
                            },
                        },
                    },
                },
            },
        });
    }

    private static string Metrics(ActivityExportBatch batch)
    {
        // Temporality 1 is DELTA. Each batch adds its own count at that batch's AcceptedAt.
        // The collector's delta_to_cumulative processor accumulates these before Prometheus remote write.
        // A fresh cumulative 1 on every row would leave the series stuck at 1.
        var sums = batch.Rows
            .GroupBy(row => (row.Attempt, row.Outcome))
            .Select(group => new
            {
                attributes = Attributes(new Dictionary<string, string>
                {
                    ["attempt"] = group.Key.Attempt,
                    ["outcome"] = group.Key.Outcome,
                }),
                asInt = group.Count().ToString(System.Globalization.CultureInfo.InvariantCulture),
                timeUnixNano = UnixNano(group.Max(row => row.AcceptedAt)),
            });
        var durations = batch.Rows.Select((row, index) => Histogram(row.DurationMs, UnixNano(row.AcceptedAt.AddTicks(index))));
        var waits = batch.Rows.Select((row, index) => Histogram(row.StorageWaitMs, UnixNano(row.AcceptedAt.AddTicks(index))));
        return JsonSerializer.Serialize(new
        {
            resourceMetrics = new[]
            {
                new
                {
                    resource = new { attributes = Attributes(batch.ResourceAttributes) },
                    scopeMetrics = new[]
                    {
                        new
                        {
                            metrics = new object[]
                            {
                                new
                                {
                                    name = ActivityExportBatch.Attempts,
                                    sum = new
                                    {
                                        aggregationTemporality = 1,
                                        isMonotonic = true,
                                        dataPoints = sums,
                                    },
                                },
                                new
                                {
                                    name = ActivityExportBatch.Duration,
                                    histogram = new { aggregationTemporality = 2, dataPoints = durations },
                                },
                                new
                                {
                                    name = ActivityExportBatch.StorageWait,
                                    histogram = new { aggregationTemporality = 2, dataPoints = waits },
                                },
                            },
                        },
                    },
                },
            },
        });
    }

    private static object Histogram(double value, string now) => new
    {
        timeUnixNano = now,
        count = "1",
        sum = value,
        bucketCounts = new[] { "0", "1" },
        explicitBounds = new[] { value },
    };

    private static string UnixNano(DateTimeOffset instant) =>
        ((instant.UtcDateTime - DateTime.UnixEpoch).Ticks * 100L).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Logs(ActivityExportBatch batch)
    {
        var records = batch.Rows.Select(row => new
        {
            timeUnixNano = UnixNano(row.AcceptedAt),
            body = new { stringValue = $"reference={batch.Reference} attempt={row.Attempt} outcome={row.Outcome}" },
            attributes = Attributes(row.Attributes.Where(pair => ActivityExportBatch.Allowlist.Contains(pair.Key))),
        });
        return JsonSerializer.Serialize(new
        {
            resourceLogs = new[]
            {
                new
                {
                    resource = new { attributes = Attributes(batch.ResourceAttributes) },
                    scopeLogs = new[]
                    {
                        new
                        {
                            scope = new { name = TelemetryExtensions.ServiceName },
                            logRecords = records,
                        },
                    },
                },
            },
        });
    }

    private static object[] Attributes(IEnumerable<KeyValuePair<string, string>> attributes) =>
        attributes.Select(pair => new
        {
            key = pair.Key,
            value = new { stringValue = pair.Value },
        }).ToArray();
}
