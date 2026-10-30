using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Quorum.App.Infrastructure.Persistence;

public sealed class QuorumDbContextFactory : IDesignTimeDbContextFactory<QuorumDbContext>
{
    public QuorumDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<QuorumDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=quorum;Username=quorum;Password=quorum")
            .Options;
        return new QuorumDbContext(options);
    }
}
