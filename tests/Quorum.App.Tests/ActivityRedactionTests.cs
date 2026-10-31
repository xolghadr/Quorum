using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Quorum.App.Features.Accounts;
using Quorum.App.Features.Groups;
using Quorum.App.Features.Shared;
using Quorum.App.Infrastructure.Logging;
using Quorum.App.Infrastructure.Persistence;
using Quorum.App.SharedKernel;
using Quorum.Tests.Support;

namespace Quorum.App.Tests;

public sealed class ActivityRedactionTests : IAsyncLifetime
{
    private const string Password = "correct-horse";

    private PostgresDatabase _database = null!;

    public async Task InitializeAsync() => _database = await PostgresDatabase.StartAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Register_and_sign_in_store_outcome_without_the_secret()
    {
        await _database.ResetAsync();
        await using var lab = QuorumLab.Open(_database.ConnectionString);
        var created = await lab.Accounts.RegisterAsync("ada@example.com", Password);
        Assert.Equal(AccountCommandStatus.Created, created.Status);
        var register = await Single(lab, "register");
        Assert.Equal(ActivityRecord.Succeeded, register.Outcome);
        Assert.Equal(created.Account!.Id.Value, register.AccountId);
        Assert.Null(register.ExportedAt);
        Assert.DoesNotContain(Password, JsonSerializer.Serialize(register));

        var wrong = await lab.Accounts.SignInAsync("ada@example.com", "not-the-password");
        Assert.Equal(AccountCommandStatus.Rejected, wrong.Status);
        var wrongRow = await Single(lab, "sign-in");
        Assert.Equal(ActivityRecord.Refused, wrongRow.Outcome);
        Assert.Equal(created.Account.Id.Value, wrongRow.AccountId);
        Assert.Null(wrongRow.Reason);
        Assert.Null(wrongRow.ExportedAt);
        Assert.DoesNotContain(Password, JsonSerializer.Serialize(wrongRow));
        Assert.DoesNotContain("not-the-password", JsonSerializer.Serialize(wrongRow));

        await _database.ResetAsync();
        await using var unknown = QuorumLab.Open(_database.ConnectionString);
        await unknown.Accounts.SignInAsync("nobody@example.com", Password);
        var unknownRow = await Single(unknown, "sign-in");
        Assert.Equal(ActivityRecord.UnknownAccount, unknownRow.Reason);
        Assert.Null(unknownRow.AccountId);
        Assert.DoesNotContain("nobody@example.com", JsonSerializer.Serialize(unknownRow));
        Assert.DoesNotContain(Password, JsonSerializer.Serialize(unknownRow));

        await unknown.Accounts.SignInAsync("  ", "");
        var blank = await unknown.Db.ActivityRecords.SingleAsync(row => row.Attempt == "sign-in" && row.Reason == null);
        Assert.Equal(ActivityRecord.Refused, blank.Outcome);
        Assert.Null(blank.Reason);
        Assert.DoesNotContain("@", JsonSerializer.Serialize(blank));
    }

    [Fact]
    public async Task Eight_succeeded_attempts_keep_eight_references_and_null_export()
    {
        await _database.ResetAsync();
        await using var lab = QuorumLab.Open(_database.ConnectionString);
        var account = (await lab.Accounts.RegisterAsync("ada@example.com", Password)).Account!;
        Assert.Equal(AccountCommandStatus.SignedIn, (await lab.Accounts.SignInAsync("ada@example.com", Password)).Status);
        var group = await lab.Groups.CreateAsync(account.Id, "North", "Ada");
        Assert.Equal(CommandStatus.Ok, group.Status);
        var groupId = new GroupId(group.Id!.Value);
        var added = await lab.Roster.AddAsync(groupId, account.Id, "Beatrice");
        Assert.Equal(CommandStatus.Refused, added.Status);
        var refusedAdd = await lab.Db.ActivityRecords.SingleAsync(row => row.Attempt == "add-member");
        Assert.Equal(ActivityRecord.Refused, refusedAdd.Outcome);
        Assert.DoesNotContain("Beatrice", JsonSerializer.Serialize(refusedAdd));
        var bea = (await lab.Accounts.RegisterAsync("bea@example.com", Password)).Account!;
        var membership = MembershipRows.Account(groupId, bea.Id, "Beatrice", lab.Clock.UtcNow);
        lab.Db.Memberships.Add(membership);
        await lab.Db.SaveChangesAsync();
        var memberId = membership.Id;
        Assert.Equal(CommandStatus.Ok, (await lab.Roster.RenameAsync(groupId, account.Id, memberId, "Bea")).Status);
        Assert.Equal(CommandStatus.Ok, (await lab.Roster.RemoveAsync(groupId, account.Id, memberId)).Status);

        var root = RoadmapFiles.WriteFixture(Path.Combine(Path.GetTempPath(), "quorum-redact-" + Guid.NewGuid().ToString("N")));
        try
        {
            Assert.Equal(0, await lab.Publish.RunAsync(root));
            var topicIds = await lab.Db.Topics.OrderBy(topic => topic.Key).Select(topic => topic.Id).Take(2).ToListAsync();
            Assert.Equal(CommandStatus.Ok, (await lab.Sessions.CreateAsync(groupId, account.Id, topicIds)).Status);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        var rows = await lab.Db.ActivityRecords.Where(row => row.Outcome == ActivityRecord.Succeeded).ToListAsync();
        Assert.Equal(8, rows.Count);
        Assert.Equal(8, rows.Select(row => row.Reference).Distinct().Count());
        Assert.All(rows, row => Assert.Null(row.ExportedAt));
        Assert.All(rows, row => Assert.True(row.DurationMs >= 0 && row.StorageWaitMs >= 0 && row.StorageWaitMs <= row.DurationMs));
        var minute = rows[0].AcceptedAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm");
        Assert.All(rows, row => Assert.Equal(minute, row.AcceptedAt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm")));
        Assert.Contains(rows, row => row.Attempt == "register" && row.AccountId == account.Id.Value);
        Assert.Contains(rows, row => row.Attempt == "create-group" && row.GroupId == groupId.Value && row.MemberId != null);
        Assert.Contains(rows, row => row.Attempt == "register" && row.AccountId == bea.Id.Value);
        Assert.DoesNotContain(rows, row => row.Attempt == "add-member");
        Assert.Contains(rows, row => row.Attempt == "rename-member" && row.MemberId == memberId.Value);
        Assert.Contains(rows, row => row.Attempt == "remove-member" && row.MemberId == memberId.Value);
        var publication = Assert.Single(rows, row => row.Attempt == "publish-catalog");
        Assert.Equal(1, publication.Tracks);
        Assert.Equal(3, publication.Topics);
        Assert.Equal(1, publication.Designs);
        Assert.All(rows.Where(row => row.Attempt != "publish-catalog"), row =>
        {
            Assert.Null(row.Tracks);
            Assert.Null(row.Topics);
            Assert.Null(row.Designs);
        });
        var stored = JsonSerializer.Serialize(rows);
        Assert.DoesNotContain(Password, stored);
        Assert.DoesNotContain("ada@example.com", stored);
        Assert.DoesNotContain("Beatrice", stored);
        Assert.DoesNotContain("North", stored);
    }

    [Fact]
    public async Task Failed_publication_stores_null_counts_and_no_page_body()
    {
        await _database.ResetAsync();
        await using var lab = QuorumLab.Open(_database.ConnectionString);
        var missing = Path.Combine(Path.GetTempPath(), "quorum-missing-" + Guid.NewGuid().ToString("N"));
        Assert.Equal(1, await lab.Publish.RunAsync(missing));
        var row = await Single(lab, "publish-catalog");
        Assert.Equal(ActivityRecord.Refused, row.Outcome);
        Assert.Null(row.Tracks);
        Assert.Null(row.Topics);
        Assert.Null(row.Designs);
        Assert.Null(row.ExportedAt);
        Assert.DoesNotContain("Must-have", JsonSerializer.Serialize(row));
    }

    private static async Task<ActivityRecord> Single(QuorumLab lab, string attempt) =>
        await lab.Db.ActivityRecords.SingleAsync(row => row.Attempt == attempt);
}
