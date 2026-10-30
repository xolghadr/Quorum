using Microsoft.EntityFrameworkCore;
using Quorum.App.Features.Accounts;
using Quorum.App.Features.Catalog;
using Quorum.App.Features.Groups;
using Quorum.App.Features.Sessions;

namespace Quorum.App.Infrastructure.Persistence;

public sealed class QuorumDbContext : DbContext
{
    public QuorumDbContext(DbContextOptions<QuorumDbContext> options) : base(options)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<CatalogPublication> CatalogPublications => Set<CatalogPublication>();
    public DbSet<Track> Tracks => Set<Track>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<TopicSection> TopicSections => Set<TopicSection>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<WorkedDesign> WorkedDesigns => Set<WorkedDesign>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<SessionAttendance> SessionAttendance => Set<SessionAttendance>();
    public DbSet<SessionVoter> SessionVoters => Set<SessionVoter>();
    public DbSet<SessionVote> SessionVotes => Set<SessionVote>();
    public DbSet<SessionTopic> SessionTopics => Set<SessionTopic>();
    public DbSet<TopicProgress> TopicProgress => Set<TopicProgress>();

    public static QuorumDbContext Create(string connectionString)
    {
        var options = new DbContextOptionsBuilder<QuorumDbContext>().UseNpgsql(connectionString).Options;
        return new QuorumDbContext(options);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(QuorumDbContext).Assembly);
    }
}
