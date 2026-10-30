using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quorum.Tests.Support;

namespace Quorum.App.Tests;

public sealed class DebugEndpointTests : IAsyncLifetime
{
    private PostgresDatabase _database = null!;

    public async Task InitializeAsync() => _database = await PostgresDatabase.StartAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Development_endpoint_stays_on_localhost_when_the_port_is_closed()
    {
        var file = RepoFile("src/Quorum.App/appsettings.Development.json");
        using var json = JsonDocument.Parse(File.ReadAllText(file));
        var root = json.RootElement;
        Assert.Equal("http://127.0.0.1:4318", root.GetProperty("Telemetry").GetProperty("OtlpEndpoint").GetString());
        Assert.Contains("Host=localhost", root.GetProperty("ConnectionStrings").GetProperty("Quorum").GetString(), StringComparison.Ordinal);

        var development = new ConfigurationBuilder().AddJsonFile(file).Build();
        Assert.Equal("http://127.0.0.1:4318", development["Telemetry:OtlpEndpoint"]);
        Assert.Contains("Host=localhost", development.GetConnectionString("Quorum"), StringComparison.Ordinal);
        Assert.DoesNotContain("otel-collector", development["Telemetry:OtlpEndpoint"], StringComparison.OrdinalIgnoreCase);
        foreach (var host in new[] { "loki", "prometheus", "tempo", "grafana" })
            Assert.DoesNotContain(host, development["Telemetry:OtlpEndpoint"]!, StringComparison.OrdinalIgnoreCase);

        await using var app = new ObservabilityApp(_database.ConnectionString, keepDevelopmentEndpoint: true);
        var config = app.Services.GetRequiredService<IConfiguration>();
        using var tcp = new TcpClient();
        try
        {
            await tcp.ConnectAsync("127.0.0.1", 4318, new CancellationTokenSource(TimeSpan.FromMilliseconds(200)).Token);
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException or TimeoutException)
        {
        }

        Assert.Equal("http://127.0.0.1:4318", config["Telemetry:OtlpEndpoint"]);
        Assert.DoesNotContain("otel-collector", config["Telemetry:OtlpEndpoint"], StringComparison.OrdinalIgnoreCase);
    }

    private static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Quorum.sln")))
                return Path.Combine(dir.FullName, relative);
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Quorum.sln was not found.");
    }
}
