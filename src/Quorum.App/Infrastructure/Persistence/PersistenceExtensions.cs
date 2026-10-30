using Microsoft.EntityFrameworkCore;

namespace Quorum.App.Infrastructure.Persistence;

public static class PersistenceExtensions
{
    public const string ConnectionStringName = "Quorum";

    public static WebApplicationBuilder AddQuorumPersistence(this WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException("Connection string Quorum is required.");
        builder.Services.AddDbContext<QuorumDbContext>(options => options.UseNpgsql(connectionString));
        return builder;
    }
}
