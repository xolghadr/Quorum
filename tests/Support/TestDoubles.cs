using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Quorum.App.Features.Accounts;
using Quorum.App.Features.Catalog.Publish;
using Quorum.App.Features.Groups;
using Quorum.App.Features.Sessions;
using Quorum.App.Infrastructure.Logging;
using Quorum.App.Infrastructure.Persistence;
using Quorum.App.SharedKernel;

namespace Quorum.Tests.Support;

public sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);

    public DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);
}

public sealed class FixedTieBreak : IFacilitatorTieBreak
{
    public MemberId Next { get; set; }

    public MemberId Choose(IReadOnlyList<MemberId> leaders)
    {
        if (!leaders.Contains(Next))
            throw new InvalidOperationException("The replacement choice is not one of the leaders.");
        return Next;
    }
}

public sealed class PostgresDatabase : IAsyncDisposable
{
    private readonly string _admin = "Host=localhost;Port=5432;Database=quorum;Username=quorum;Password=quorum";
    private string _name = "";

    public string ConnectionString { get; private set; } = "";

    public static async Task<PostgresDatabase> StartAsync()
    {
        var database = new PostgresDatabase();
        database._name = "q_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(database._admin);
        await admin.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{database._name}\"", admin);
        await create.ExecuteNonQueryAsync();
        database.ConnectionString = $"Host=localhost;Port=5432;Database={database._name};Username=quorum;Password=quorum";
        await using var db = QuorumDbContext.Create(database.ConnectionString);
        await db.Database.MigrateAsync();
        return database;
    }

    public QuorumDbContext CreateContext() => QuorumDbContext.Create(ConnectionString);

    public async Task ResetAsync()
    {
        await using var db = CreateContext();
        await db.Database.ExecuteSqlRawAsync(
            """
            DO $$ DECLARE row record;
            BEGIN
              FOR row IN SELECT tablename FROM pg_tables WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory'
              LOOP
                EXECUTE 'TRUNCATE TABLE ' || quote_ident(row.tablename) || ' CASCADE';
              END LOOP;
            END $$;
            """);
    }

    public async ValueTask DisposeAsync()
    {
        if (_name.Length == 0)
            return;
        await using var admin = new NpgsqlConnection(_admin);
        await admin.OpenAsync();
        await using (var kill = new NpgsqlCommand(
            "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @name AND pid <> pg_backend_pid()",
            admin))
        {
            kill.Parameters.AddWithValue("name", _name);
            await kill.ExecuteNonQueryAsync();
        }

        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_name}\"", admin);
        await drop.ExecuteNonQueryAsync();
    }
}

public sealed class QuorumLab : IAsyncDisposable
{
    public required IServiceScope Scope { get; init; }
    public required ServiceProvider Provider { get; init; }
    public required QuorumDbContext Db { get; init; }
    public required FakeClock Clock { get; init; }
    public required FixedTieBreak Ties { get; init; }
    public required AccountCommands Accounts { get; init; }
    public required GroupCommands Groups { get; init; }
    public required RosterCommands Roster { get; init; }
    public required SessionCommands Sessions { get; init; }
    public required Election Election { get; init; }
    public required PublishCatalog Publish { get; init; }

    public static QuorumLab Open(string connectionString, FakeClock? clock = null)
    {
        var time = clock ?? new FakeClock();
        var ties = new FixedTieBreak();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IClock>(time);
        services.AddSingleton<IFacilitatorTieBreak>(ties);
        services.AddSingleton<RoadmapParser>();
        services.AddSingleton<SafeLog>();
        services.AddSingleton<IPasswordHasher<Account>, PasswordHasher<Account>>();
        services.AddDbContext<QuorumDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<AccountCommands>();
        services.AddScoped<GroupCommands>();
        services.AddScoped<RosterCommands>();
        services.AddScoped<SessionCommands>();
        services.AddScoped<Election>();
        services.AddScoped<PublishCatalog>();
        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuorumDbContext>();
        return new QuorumLab
        {
            Scope = scope,
            Provider = provider,
            Db = db,
            Clock = time,
            Ties = ties,
            Accounts = scope.ServiceProvider.GetRequiredService<AccountCommands>(),
            Groups = scope.ServiceProvider.GetRequiredService<GroupCommands>(),
            Roster = scope.ServiceProvider.GetRequiredService<RosterCommands>(),
            Sessions = scope.ServiceProvider.GetRequiredService<SessionCommands>(),
            Election = scope.ServiceProvider.GetRequiredService<Election>(),
            Publish = scope.ServiceProvider.GetRequiredService<PublishCatalog>(),
        };
    }

    public async ValueTask DisposeAsync()
    {
        Scope.Dispose();
        await Provider.DisposeAsync();
    }
}

public static class RoadmapFiles
{
    public static string? FindSeniorBackend()
    {
        var configured = Environment.GetEnvironmentVariable("QUORUM_ROADMAP");
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(Path.Combine(configured, "tracks")))
            return configured;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Quorum.sln")))
            {
                var sibling = Path.GetFullPath(Path.Combine(dir.FullName, "..", "software-engineer-roadmap"));
                if (Directory.Exists(Path.Combine(sibling, "tracks")))
                    return sibling;
            }

            dir = dir.Parent;
        }

        return null;
    }

    public static string WriteFixture(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "tracks"));
        Directory.CreateDirectory(Path.Combine(root, "designs"));
        File.WriteAllText(Path.Combine(root, "tracks", "01-sample.md"), """
            # Sample track

            ## Real topic

            **Level:** Must-have

            **You can:** explain the fixture.

            ### In one minute

            One minute text.

            ### Review

            1. First question?
            2.
            3. First question?
            4. Second question?

            ### In an interview

            1. How does it fail?
            2. How does it fail?

            ## Good to have

            This heading is not a topic.

            ### Nested topic

            **Level:** Good-to-have

            **You can:** do the extra.

            Body prose of the nested topic.

            ## Paragraph interview

            **Level:** Must-have

            **You can:** speak it.

            ### In an interview

            This is one prompt about queues.
            """);
        File.WriteAllText(Path.Combine(root, "designs", "sample-design.md"), """
            # Design: Sample

            **Level:** Must-have

            The body of the design.
            """);
        File.WriteAllText(Path.Combine(root, "README.md"), """
            1. [Sample](designs/sample-design.md)
            """);
        return root;
    }

    public static string WriteBadTopic(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "tracks"));
        Directory.CreateDirectory(Path.Combine(root, "designs"));
        File.WriteAllText(Path.Combine(root, "tracks", "01-bad.md"), """
            # Bad track

            ## 

            **Level:** Must-have
            """);
        return root;
    }

    public static string WriteSessionCatalog(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "tracks"));
        Directory.CreateDirectory(Path.Combine(root, "designs"));
        File.WriteAllText(Path.Combine(root, "tracks", "01-alpha.md"), """
            # Alpha track

            ## Alpha Signal Topic

            **Level:** Must-have

            **You can:** explain alpha.

            ### In one minute

            QUORUM_FIXTURE_SENTENCE_ALPHA

            ## Beta Signal Topic

            **Level:** Must-have

            **You can:** explain beta.

            ### In one minute

            Beta body.
            """);
        File.WriteAllText(Path.Combine(root, "tracks", "02-gamma.md"), """
            # Gamma track

            ## Gamma Signal Topic

            **Level:** Must-have

            **You can:** explain gamma.
            """);
        File.WriteAllText(Path.Combine(root, "tracks", "03-empty.md"), """
            # Empty track

            No topics live here.
            """);
        File.WriteAllText(Path.Combine(root, "designs", "sample-design.md"), """
            # Design: Sample

            **Level:** Must-have

            A worked design that cannot be a session topic.
            """);
        return root;
    }
}
