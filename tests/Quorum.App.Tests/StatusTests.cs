using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Quorum.App.Infrastructure.Auth;
using Quorum.App.Infrastructure.Logging;
using Quorum.App.Infrastructure.Persistence;
using Quorum.Tests.Support;

namespace Quorum.App.Tests;

public sealed class StatusTests : IAsyncLifetime
{
    private PostgresDatabase _database = null!;

    public async Task InitializeAsync() => _database = await PostgresDatabase.StartAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public void Probe_defaults_are_five_seconds_and_two_seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), HealthMonitor.Interval);
        Assert.Equal(TimeSpan.FromSeconds(2), HealthMonitor.ProbeTimeout);
    }

    [Fact]
    public async Task Anonymous_status_follows_the_probe_and_stays_local()
    {
        await using var app = new ObservabilityApp(_database.ConnectionString);
        using var client = app.CreateClient(new() { AllowAutoRedirect = false });
        var first = await client.GetAsync("/status");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Null(first.Headers.Location);
        var body = await first.Content.ReadAsStringAsync();
        Assert.Contains("not ready", body, StringComparison.Ordinal);
        Assert.Contains("stored data down", body, StringComparison.Ordinal);
        Assert.Contains("local use only", body, StringComparison.Ordinal);
        Assert.DoesNotContain("ready for a group", body, StringComparison.Ordinal);
        AssertPrivateStatus(body);
        Assert.DoesNotContain(AuthExtensions.CookieName, body, StringComparison.Ordinal);

        var health = app.Services.GetRequiredService<HealthMonitor>();
        await health.ProbeAsync(false);
        Assert.Empty(await HealthRows(app));

        await health.ProbeAsync(true);
        Assert.Equal(["health-ready"], await HealthRows(app));
        await health.ProbeAsync(true);
        Assert.Equal(["health-ready"], await HealthRows(app));

        var export = new RecordingExport { Throw = true };
        await using var withCollectorDown = new ObservabilityApp(_database.ConnectionString, export: export);
        var monitor = withCollectorDown.Services.GetRequiredService<HealthMonitor>();
        await monitor.ProbeAsync(true);
        using var readyClient = withCollectorDown.CreateClient(new() { AllowAutoRedirect = false });
        var ready = await readyClient.GetStringAsync("/status");
        Assert.Contains("ready", ready, StringComparison.Ordinal);
        Assert.DoesNotContain("not ready", ready, StringComparison.Ordinal);
        Assert.Contains("stored data reachable", ready, StringComparison.Ordinal);
        Assert.Contains("local use only", ready, StringComparison.Ordinal);
        Assert.DoesNotContain("ready for a group", ready, StringComparison.Ordinal);
        AssertPrivateStatus(ready);

        await monitor.ProbeAsync(false);
        var down = await readyClient.GetStringAsync("/status");
        Assert.Contains("not ready", down, StringComparison.Ordinal);
        Assert.Contains("stored data down", down, StringComparison.Ordinal);
        Assert.DoesNotContain("ready for a group", down, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, (await readyClient.GetAsync("/status")).StatusCode);
    }

    [Fact]
    public async Task Replaced_settings_say_ready_for_a_group_after_the_probe()
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Quorum"] = "Host=localhost;Port=5432;Database=quorum;Username=quorum;Password=replaced-db",
            ["Telemetry:CollectorPassword"] = "replaced-collector",
            ["Telemetry:GrafanaAdminPassword"] = "replaced-grafana",
            ["Telemetry:PrometheusPassword"] = "replaced-prometheus",
            ["PublicBaseUrl"] = "http://groups.example",
        };
        await using var app = new ObservabilityApp(_database.ConnectionString, settings);
        var health = app.Services.GetRequiredService<HealthMonitor>();
        await health.ProbeAsync(true);
        using var client = app.CreateClient(new() { AllowAutoRedirect = false });
        var body = await client.GetStringAsync("/status");
        Assert.Contains("ready for a group", body, StringComparison.Ordinal);
        Assert.Contains("stored data reachable", body, StringComparison.Ordinal);
        Assert.DoesNotContain("not ready", body, StringComparison.Ordinal);
        Assert.DoesNotContain("local use only", body, StringComparison.Ordinal);
        Assert.DoesNotContain("replaced-db", body, StringComparison.Ordinal);
        Assert.DoesNotContain("replaced-collector", body, StringComparison.Ordinal);
        Assert.DoesNotContain("quorum", body, StringComparison.Ordinal);
        AssertPrivateStatus(body);
    }

    private static async Task<List<string>> HealthRows(ObservabilityApp app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<QuorumDbContext>();
        return await db.ActivityRecords
            .Where(row => row.Attempt == "health-ready" || row.Attempt == "health-not-ready")
            .Select(row => row.Attempt)
            .ToListAsync();
    }

    private static void AssertPrivateStatus(string body)
    {
        Assert.DoesNotContain("grafana", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@", body, StringComparison.Ordinal);
        Assert.DoesNotContain("rubric", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("score", body, StringComparison.OrdinalIgnoreCase);
    }
}
