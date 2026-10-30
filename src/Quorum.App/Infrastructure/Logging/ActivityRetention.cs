using Microsoft.EntityFrameworkCore;
using Quorum.App.Infrastructure.Persistence;
using Quorum.App.SharedKernel;

namespace Quorum.App.Infrastructure.Logging;

public sealed class ActivityRetention : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    public static readonly TimeSpan Keep = TimeSpan.FromDays(14);

    private readonly IServiceScopeFactory _scopes;
    private readonly IClock _clock;

    public ActivityRetention(IServiceScopeFactory scopes, IClock clock)
    {
        _scopes = scopes;
        _clock = clock;
    }

    public async Task DeleteExpiredAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<QuorumDbContext>();
        var cutoff = _clock.UtcNow - Keep;
        await db.ActivityRecords
            .Where(row => row.AcceptedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Interval, stoppingToken);
                await DeleteExpiredAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // The next hour tries again. Group rows are not part of this delete.
            }
        }
    }
}
