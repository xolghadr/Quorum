using Npgsql;

namespace Quorum.App.Infrastructure.Logging;

public static class LocalOnly
{
    public const string PrintedPassword = "quorum";

    public static bool IsLocalOnly(IConfiguration configuration)
    {
        if (Password(configuration.GetConnectionString(Persistence.PersistenceExtensions.ConnectionStringName)) == PrintedPassword)
            return true;
        if (configuration["Telemetry:CollectorPassword"] == PrintedPassword)
            return true;
        if (configuration["Telemetry:GrafanaAdminPassword"] == PrintedPassword)
            return true;
        if (configuration["Telemetry:PrometheusPassword"] == PrintedPassword)
            return true;
        if (!Uri.TryCreate(configuration["PublicBaseUrl"], UriKind.Absolute, out var uri))
            return true;
        return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || uri.IdnHost == "127.0.0.1";
    }

    private static string? Password(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return PrintedPassword;
        try
        {
            return new NpgsqlConnectionStringBuilder(connectionString).Password;
        }
        catch (ArgumentException)
        {
            return PrintedPassword;
        }
    }
}
