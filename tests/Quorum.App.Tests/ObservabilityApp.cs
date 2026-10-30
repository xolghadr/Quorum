using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quorum.App.Infrastructure.Logging;
using Quorum.App.Infrastructure.Persistence;
using Quorum.App.SharedKernel;

namespace Quorum.App.Tests;

public sealed class ObservabilityApp : WebApplicationFactory<Program>
{
    private readonly string _connection;
    private readonly Dictionary<string, string?> _settings;
    private readonly IActivityExport? _export;

    public ObservabilityApp(
        string connection,
        Dictionary<string, string?>? settings = null,
        IActivityExport? export = null,
        bool keepDevelopmentEndpoint = false)
    {
        _connection = connection;
        _settings = settings ?? new Dictionary<string, string?>();
        _settings.TryAdd("ConnectionStrings:Quorum", connection);
        _settings.TryAdd("Telemetry:ProbeLoop", "false");
        if (!keepDevelopmentEndpoint)
            _settings.TryAdd("Telemetry:OtlpEndpoint", "");
        _export = export;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(_settings));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<QuorumDbContext>>();
            services.RemoveAll<QuorumDbContext>();
            services.AddDbContext<QuorumDbContext>(options => options.UseNpgsql(_connection));
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(new Quorum.Tests.Support.FakeClock());
            if (_export is not null)
            {
                services.RemoveAll<IActivityExport>();
                services.AddSingleton(_export);
            }
        });
    }
}

public sealed class ProviderScopes(ServiceProvider provider) : IServiceScopeFactory
{
    public IServiceScope CreateScope() => provider.CreateScope();
}

public sealed class RecordingExport : IActivityExport
{
    public List<ActivityExportBatch> Batches { get; } = [];
    public bool Accept { get; set; } = true;
    public bool Throw { get; set; }

    public Task<bool> SendAsync(ActivityExportBatch batch, CancellationToken cancellationToken)
    {
        Batches.Add(batch);
        if (Throw)
            throw new IOException("The collector refused the batch.");
        return Task.FromResult(Accept);
    }
}
