using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Quorum.App.Features.Accounts;
using Quorum.App.Features.Shared;
using Quorum.App.Infrastructure.Logging;
using Quorum.App.Infrastructure.Persistence;
using Quorum.App.SharedKernel;
using Quorum.Tests.Support;

namespace Quorum.App.Tests;

public sealed class ActivityDurabilityTests : IAsyncLifetime
{
    private const string Password = "correct-horse";

    private PostgresDatabase _database = null!;

    public async Task InitializeAsync() => _database = await PostgresDatabase.StartAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Failed_export_retries_once_and_retention_keeps_the_group()
    {
        await _database.ResetAsync();
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero) };
        await using var lab = QuorumLab.Open(_database.ConnectionString, clock);
        var account = (await lab.Accounts.RegisterAsync("ada@example.com", Password)).Account!;
        var group = await lab.Groups.CreateAsync(account.Id, "North", "Ada");
        var groupId = new GroupId(group.Id!.Value);
        var beaAccount = (await lab.Accounts.RegisterAsync("bea@example.com", Password)).Account!;
        var bea = MembershipRows.Account(groupId, beaAccount.Id, "Bea", lab.Clock.UtcNow);
        lab.Db.Memberships.Add(bea);
        await lab.Db.SaveChangesAsync();
        var root = RoadmapFiles.WriteFixture(Path.Combine(Path.GetTempPath(), "quorum-keep-" + Guid.NewGuid().ToString("N")));
        Guid sessionId;
        try
        {
            Assert.Equal(0, await lab.Publish.RunAsync(root));
            var topicIds = await lab.Db.Topics.OrderBy(topic => topic.Key).Select(topic => topic.Id).Take(2).ToListAsync();
            var created = await lab.Sessions.CreateAsync(groupId, account.Id, topicIds);
            sessionId = created.Id!.Value;
            var adaMember = await lab.Db.Memberships.SingleAsync(member => member.AccountId == account.Id);
            var id = new SessionId(sessionId);
            Assert.Equal(CommandStatus.Ok, (await lab.Election.SaveAttendanceAsync(groupId, account.Id, id, adaMember.Id, true)).Status);
            Assert.Equal(CommandStatus.Ok, (await lab.Election.SaveAttendanceAsync(groupId, beaAccount.Id, id, bea.Id, true)).Status);
            Assert.Equal(CommandStatus.Ok, (await lab.Election.CastVoteAsync(groupId, account.Id, id, adaMember.Id, adaMember.Id)).Status);
            Assert.Equal(CommandStatus.Ok, (await lab.Election.CastVoteAsync(groupId, beaAccount.Id, id, bea.Id, adaMember.Id)).Status);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        var oldReference = (await lab.Db.ActivityRecords.SingleAsync(row => row.Attempt == "register" && row.AccountId == account.Id.Value)).Reference;
        var export = new RecordingExport { Throw = true };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Telemetry:OtlpEndpoint"] = "http://127.0.0.1:4318",
        }).Build();
        var exporter = new ActivityExporter(new ProviderScopes(lab.Provider), export, config, clock);
        await exporter.ExportOnceAsync();
        lab.Db.ChangeTracker.Clear();
        var pending = await lab.Db.ActivityRecords.SingleAsync(row => row.Reference == oldReference);
        Assert.Equal(ActivityRecord.Succeeded, pending.Outcome);
        Assert.Null(pending.ExportedAt);
        Assert.Equal(1, await lab.Db.ActivityRecords.CountAsync(row => row.Reference == oldReference));

        export.Throw = false;
        export.Accept = true;
        await exporter.ExportOnceAsync();
        lab.Db.ChangeTracker.Clear();
        var exported = await lab.Db.ActivityRecords.SingleAsync(row => row.Reference == oldReference);
        Assert.Equal(pending.Id, exported.Id);
        Assert.Equal(clock.UtcNow, exported.ExportedAt);
        Assert.Equal(ActivityRecord.Succeeded, exported.Outcome);

        clock.UtcNow = clock.UtcNow.AddSeconds(1);
        Assert.Equal(AccountCommandStatus.SignedIn, (await lab.Accounts.SignInAsync("ada@example.com", Password)).Status);
        var boundary = await lab.Db.ActivityRecords.OrderByDescending(row => row.AcceptedAt).FirstAsync(row => row.Attempt == "sign-in");
        clock.UtcNow = new DateTimeOffset(2026, 3, 1, 12, 0, 1, TimeSpan.Zero).AddDays(14);

        var retention = new ActivityRetention(new ProviderScopes(lab.Provider), clock);
        await retention.DeleteExpiredAsync();
        lab.Db.ChangeTracker.Clear();

        Assert.Equal(0, await lab.Db.ActivityRecords.CountAsync(row => row.Reference == oldReference));
        Assert.Equal(1, await lab.Db.ActivityRecords.CountAsync(row => row.Reference == boundary.Reference));
        Assert.Equal(1, await lab.Db.Groups.CountAsync(row => row.Id == groupId));
        Assert.Equal(1, await lab.Db.Sessions.CountAsync(row => row.Id == new SessionId(sessionId)));
        Assert.True(await lab.Db.Memberships.AnyAsync(row => row.GroupId == groupId));
        Assert.True(await lab.Db.SessionVotes.AnyAsync(row => row.SessionId == new SessionId(sessionId)));
        Assert.Equal(1, await lab.Db.CatalogPublications.CountAsync());

        var missing = new StringWriter();
        Assert.Equal(1, ActivityCommand.Execute(lab.Provider, oldReference, missing));
        Assert.Equal("not found\n", missing.ToString().Replace("\r\n", "\n"));
        Assert.Equal(1, ActivityCommand.Execute(lab.Provider, "ZZZZZZZZ", new StringWriter()));

        var printed = new StringWriter();
        Assert.Equal(0, ActivityCommand.Execute(lab.Provider, boundary.Reference, printed));
        var text = printed.ToString().Replace("\r\n", "\n");
        Assert.Contains("reference: " + boundary.Reference, text, StringComparison.Ordinal);
        Assert.Contains("attempt: sign-in", text, StringComparison.Ordinal);
        Assert.Contains("outcome: succeeded", text, StringComparison.Ordinal);
        Assert.Contains("reason: -", text, StringComparison.Ordinal);
        Assert.Contains("tracks: -", text, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, text, StringComparison.Ordinal);
        Assert.DoesNotContain("ada@example.com", text, StringComparison.Ordinal);
        Assert.DoesNotContain("North", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Must-have", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_skips_a_row_older_than_fourteen_days_and_sends_the_boundary()
    {
        await _database.ResetAsync();
        var clock = new FakeClock { UtcNow = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero) };
        await using var lab = QuorumLab.Open(_database.ConnectionString, clock);
        var account = (await lab.Accounts.RegisterAsync("ada@example.com", Password)).Account!;
        Assert.NotEqual(Guid.Empty, account.Id.Value);
        var acceptedAt = (await lab.Db.ActivityRecords.SingleAsync(row => row.Attempt == "register")).AcceptedAt;

        clock.UtcNow = acceptedAt.AddDays(14).AddSeconds(1);
        var export = new RecordingExport();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Telemetry:OtlpEndpoint"] = "http://127.0.0.1:4318",
        }).Build();
        var exporter = new ActivityExporter(new ProviderScopes(lab.Provider), export, config, clock);
        await exporter.ExportOnceAsync();
        Assert.Empty(export.Batches);
        lab.Db.ChangeTracker.Clear();
        Assert.Null((await lab.Db.ActivityRecords.SingleAsync()).ExportedAt);

        clock.UtcNow = acceptedAt.AddDays(14);
        await exporter.ExportOnceAsync();
        var batch = Assert.Single(export.Batches);
        Assert.Equal(acceptedAt, Assert.Single(batch.Rows).AcceptedAt);
        lab.Db.ChangeTracker.Clear();
        Assert.Equal(clock.UtcNow, (await lab.Db.ActivityRecords.SingleAsync()).ExportedAt);
    }
}
