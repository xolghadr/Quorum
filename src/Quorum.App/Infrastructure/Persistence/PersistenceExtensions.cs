using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Quorum.App.Infrastructure.Logging;

namespace Quorum.App.Infrastructure.Persistence;

public static class PersistenceExtensions
{
    public const string ConnectionStringName = "Quorum";

    public static WebApplicationBuilder AddQuorumPersistence(this WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException("Connection string Quorum is required.");
        builder.Services.AddSingleton<StorageWaitInterceptor>();
        builder.Services.AddDbContext<QuorumDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString);
            options.AddInterceptors(sp.GetRequiredService<StorageWaitInterceptor>());
        });
        return builder;
    }

    public static void MigrateQuorumDatabase(this WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<QuorumDbContext>();
            db.Database.Migrate();
        }
        catch (Exception ex) when (IsUnreachable(ex))
        {
            app.Logger.LogWarning("Stored data is unreachable. The process will continue.");
        }
    }

    private static bool IsUnreachable(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SocketException or TimeoutException or IOException)
                return true;
            if (current is NpgsqlException npgsql && (npgsql.IsTransient || npgsql.SqlState is "08001" or "08006" or "08004" or "57P01"))
                return true;
        }

        return false;
    }
}
