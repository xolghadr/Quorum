using Microsoft.EntityFrameworkCore;
using Npgsql;
using Quorum.App.Features.Accounts;
using Quorum.App.Features.Groups;
using Quorum.App.Features.Shared;
using Quorum.Tests.Support;

namespace Quorum.Groups.Tests;

public class AccountGroupTests : IAsyncLifetime
{
    private const string Password = "correct-horse";

    private PostgresDatabase _database = null!;

    public async Task InitializeAsync() => _database = await PostgresDatabase.StartAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Email_is_unique_ignoring_case_and_the_password_is_a_salted_hash()
    {
        await using var lab = QuorumLab.Open(_database.ConnectionString);

        Assert.Equal(AccountCommandStatus.Invalid, (await lab.Accounts.RegisterAsync("ada@example.com", new string('p', 9))).Status);
        Assert.Equal(AccountCommandStatus.Invalid, (await lab.Accounts.RegisterAsync("ada@example.com", new string('p', 129))).Status);
        Assert.Equal(0, await lab.Db.Accounts.CountAsync());

        var created = await lab.Accounts.RegisterAsync("  Ada@Example.com  ", Password);
        Assert.Equal(AccountCommandStatus.Created, created.Status);
        var again = await lab.Accounts.RegisterAsync("ada@example.com", Password);
        Assert.Equal(AccountCommandStatus.DuplicateEmail, again.Status);
        Assert.Null(again.Account);

        var account = await lab.Db.Accounts.SingleAsync();
        Assert.Equal("ada@example.com", account.Email);
        Assert.NotEqual(Password, account.PasswordHash);
        Assert.DoesNotContain(Password, account.PasswordHash, StringComparison.Ordinal);

        var second = await lab.Accounts.RegisterAsync("bea@example.com", Password);
        Assert.Equal(AccountCommandStatus.Created, second.Status);
        Assert.NotEqual(account.PasswordHash, second.Account!.PasswordHash);

        var longEnough = await lab.Accounts.RegisterAsync("max@example.com", new string('p', 128));
        Assert.Equal(AccountCommandStatus.Created, longEnough.Status);
        var shortest = await lab.Accounts.RegisterAsync("min@example.com", new string('p', 10));
        Assert.Equal(AccountCommandStatus.Created, shortest.Status);

        Assert.Equal(
            ["CreatedAt", "Email", "Id", "PasswordHash"],
            typeof(Account).GetProperties().Select(property => property.Name).OrderBy(name => name).ToArray());

        var columns = new List<string>();
        await using var connection = new NpgsqlConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'Accounts'
            """,
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(0));
        Assert.DoesNotContain(columns, column => column.Contains("salt", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(AccountCommandStatus.SignedIn, (await lab.Accounts.SignInAsync("Ada@Example.com", Password)).Status);
        Assert.Equal(AccountCommandStatus.Rejected, (await lab.Accounts.SignInAsync("Ada@Example.com", "wrong-password")).Status);
        Assert.Equal(AccountCommandStatus.Rejected, (await lab.Accounts.SignInAsync("missing@example.com", Password)).Status);
    }

    [Fact]
    public async Task A_second_group_copies_nothing_and_the_ninth_member_is_refused()
    {
        await using var lab = QuorumLab.Open(_database.ConnectionString);
        var owner = (await lab.Accounts.RegisterAsync("ada@example.com", Password)).Account!;
        var root = RoadmapFiles.WriteSessionCatalog(Path.Combine(Path.GetTempPath(), "quorum-groups-" + Guid.NewGuid().ToString("N")));
        try
        {
            Assert.Equal(0, await lab.Publish.RunAsync(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        Assert.Equal(CommandStatus.Refused, (await lab.Groups.CreateAsync(owner.Id, "  ", "Ada")).Status);
        Assert.Equal(CommandStatus.Refused, (await lab.Groups.CreateAsync(owner.Id, new string('n', 61), "Ada")).Status);
        Assert.Equal(CommandStatus.Refused, (await lab.Groups.CreateAsync(owner.Id, "North", "  ")).Status);
        Assert.Equal(CommandStatus.Refused, (await lab.Groups.CreateAsync(owner.Id, "North", new string('d', 41))).Status);
        Assert.Equal(0, await lab.Db.Groups.CountAsync());

        var first = await lab.Groups.CreateAsync(owner.Id, "  North  ", "  Ada  ");
        Assert.Equal(CommandStatus.Ok, first.Status);
        var north = new Quorum.App.SharedKernel.GroupId(first.Id!.Value);
        var ada = await lab.Db.Memberships.SingleAsync();
        Assert.Equal("North", (await lab.Db.Groups.SingleAsync()).Name);
        Assert.Equal("Ada", ada.DisplayName);
        Assert.Equal("ada", ada.DisplayNameKey);

        var topics = await lab.Db.Topics.OrderBy(topic => topic.Key).Take(2).Select(topic => topic.Id).ToListAsync();
        var session = await lab.Sessions.CreateAsync(north, owner.Id, topics);
        Assert.Equal(CommandStatus.Ok, session.Status);
        Assert.Equal(2, await lab.Db.TopicProgress.CountAsync(progress => progress.GroupId == north && progress.PickCount == 1));

        var second = await lab.Groups.CreateAsync(owner.Id, new string('s', 60), new string('B', 40));
        Assert.Equal(CommandStatus.Ok, second.Status);
        var south = new Quorum.App.SharedKernel.GroupId(second.Id!.Value);
        var southMembers = await lab.Db.Memberships.Where(member => member.GroupId == south).ToListAsync();
        var southCreator = Assert.Single(southMembers);
        Assert.Equal(new string('B', 40), southCreator.DisplayName);
        Assert.Equal(0, await lab.Db.Sessions.CountAsync(row => row.GroupId == south));
        Assert.Equal(0, await lab.Db.TopicProgress.CountAsync(progress => progress.GroupId == south));
        Assert.Equal(1, await lab.Db.Sessions.CountAsync(row => row.GroupId == north));
        Assert.Equal(2, await lab.Db.TopicProgress.CountAsync(progress => progress.GroupId == north));

        for (var number = 2; number <= Group.MemberMax; number++)
            Assert.Equal(CommandStatus.Ok, (await lab.Roster.AddAsync(north, owner.Id, "Member " + number)).Status);
        Assert.Equal(CommandStatus.Refused, (await lab.Roster.AddAsync(north, owner.Id, "Member 9")).Status);
        Assert.Equal(Group.MemberMax, await lab.Db.Memberships.CountAsync(member => member.GroupId == north && member.RemovedAt == null));
        Assert.Equal(1, await lab.Db.Memberships.CountAsync(member => member.GroupId == south && member.RemovedAt == null));
    }
}
