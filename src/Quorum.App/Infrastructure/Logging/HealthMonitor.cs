using Npgsql;
using Quorum.App.Infrastructure.Persistence;
using Quorum.App.SharedKernel;

namespace Quorum.App.Infrastructure.Logging;

public sealed record HealthSnapshot(
    string Readiness,
    string StoredData,
    string Use,
    DateTimeOffset? CheckedAt);

public sealed class HealthMonitor : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);

    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _configuration;
    private readonly IClock _clock;
    private readonly SafeLog _log;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _ready;
    private DateTimeOffset? _checkedAt;
    private bool _startWritten;

    public HealthMonitor(IServiceScopeFactory scopes, IConfiguration configuration, IClock clock, SafeLog log)
    {
        _scopes = scopes;
        _configuration = configuration;
        _clock = clock;
        _log = log;
    }

    public HealthSnapshot Snapshot()
    {
        var ready = _ready;
        var local = LocalOnly.IsLocalOnly(_configuration);
        return new HealthSnapshot(
            ready ? "ready" : "not-ready",
            ready ? "reachable" : "down",
            ready && !local ? "ready-for-a-group" : "local-use-only",
            _checkedAt);
    }

    public async Task ProbeAsync(CancellationToken cancellationToken = default)
    {
        var reached = false;
        try
        {
            var raw = _configuration.GetConnectionString(PersistenceExtensions.ConnectionStringName)
                ?? throw new InvalidOperationException("Connection string Quorum is required.");
            var tuned = new NpgsqlConnectionStringBuilder(raw)
            {
                Timeout = (int)ProbeTimeout.TotalSeconds,
                CommandTimeout = (int)ProbeTimeout.TotalSeconds,
            };
            await using var connection = new NpgsqlConnection(tuned.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT 1", connection)
            {
                CommandTimeout = (int)ProbeTimeout.TotalSeconds,
            };
            await command.ExecuteScalarAsync(cancellationToken);
            reached = true;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            reached = false;
        }

        await ApplyAsync(reached, cancellationToken);
    }

    public Task ProbeAsync(bool reached, CancellationToken cancellationToken = default) =>
        ApplyAsync(reached, cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await TryStartAsync(stoppingToken);
        if (string.Equals(_configuration["Telemetry:ProbeLoop"], "false", StringComparison.OrdinalIgnoreCase))
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProbeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // The probe records stored data down and keeps the process up.
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

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _log.Begin();
            await _log.RecordAsync(new ActivityWrite("application-stop", true), cancellationToken);
            _log.Finish();
        }
        catch
        {
            // A kill that cannot write leaves the earlier rows and does not wait on the collector.
        }

        await base.StopAsync(cancellationToken);
    }

    private async Task TryStartAsync(CancellationToken cancellationToken)
    {
        if (_startWritten)
            return;
        try
        {
            _log.Begin();
            await _log.RecordAsync(new ActivityWrite("application-start", true), cancellationToken);
            _log.Finish();
            _startWritten = true;
        }
        catch
        {
            // Stored data is down. A later pass writes the one start row.
        }
    }

    private async Task ApplyAsync(bool reached, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var changed = _checkedAt is null ? reached : _ready != reached;
            _ready = reached;
            _checkedAt = _clock.UtcNow;
            if (!changed)
                return;
            if (!_startWritten)
                await TryStartAsync(cancellationToken);
            _log.Begin();
            await _log.RecordAsync(new ActivityWrite(reached ? "health-ready" : "health-not-ready", true), cancellationToken);
            _log.Finish();
        }
        finally
        {
            _gate.Release();
        }
    }
}
