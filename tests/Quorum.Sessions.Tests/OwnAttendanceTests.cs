using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Quorum.App.Features.Groups;
using Quorum.App.Features.Sessions;
using Quorum.App.Features.Shared;
using Quorum.App.Infrastructure.Logging;
using Quorum.App.SharedKernel;
using Quorum.Tests.Support;

namespace Quorum.Sessions.Tests;

public class OwnAttendanceTests : IAsyncLifetime
{
    private const string Password = "correct-horse";

    private PostgresDatabase _database = null!;

    public async Task InitializeAsync() => _database = await PostgresDatabase.StartAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Members_mark_and_vote_only_for_themselves()
    {
        await using var lab = QuorumLab.Open(_database.ConnectionString);
        var topics = await Publish(lab);
        var alpha = topics["01-alpha/alpha-signal-topic"];
        var beta = topics["01-alpha/beta-signal-topic"];
        var owner = (await lab.Accounts.RegisterAsync("ada@example.com", Password)).Account!;
        var north = new GroupId((await lab.Groups.CreateAsync(owner.Id, "North", "Ada")).Id!.Value);
        var beaAccount = (await lab.Accounts.RegisterAsync("bea@example.com", Password)).Account!;
        var camAccount = (await lab.Accounts.RegisterAsync("cam@example.com", Password)).Account!;
        var bea = MembershipRows.Account(north, beaAccount.Id, "Bea", lab.Clock.UtcNow);
        var cam = MembershipRows.Account(north, camAccount.Id, "Cam", lab.Clock.UtcNow);
        var legacy = MembershipRows.NameOnly(north, "Legacy", lab.Clock.UtcNow);
        lab.Db.Memberships.AddRange(bea, cam, legacy);
        await lab.Db.SaveChangesAsync();
        var ada = await lab.Db.Memberships.SingleAsync(member => member.AccountId == owner.Id);
        var sessionId = new SessionId((await lab.Sessions.CreateAsync(north, owner.Id, [alpha, beta])).Id!.Value);

        Assert.Equal(CommandStatus.Refused, (await lab.Election.SaveAttendanceAsync(north, beaAccount.Id, sessionId, ada.Id, true)).Status);
        Assert.Equal(CommandStatus.Refused, (await lab.Election.SaveAttendanceAsync(north, beaAccount.Id, sessionId, legacy.Id, true)).Status);
        Assert.Equal(CommandStatus.Refused, (await lab.Election.SaveAttendanceAsync(north, beaAccount.Id, sessionId, MemberId.New(), true)).Status);
        Assert.Empty(await Present(lab, sessionId));

        Assert.Equal(CommandStatus.Ok, (await lab.Election.SaveAttendanceAsync(north, beaAccount.Id, sessionId, bea.Id, true)).Status);
        Assert.Equal(CommandStatus.Ok, (await lab.Election.SaveAttendanceAsync(north, beaAccount.Id, sessionId, bea.Id, true)).Status);
        Assert.Equal([bea.Id], await Present(lab, sessionId));
        Assert.DoesNotContain(ada.Id, await Present(lab, sessionId));
        Assert.Equal(CommandStatus.Refused, (await lab.Election.CastVoteAsync(north, beaAccount.Id, sessionId, bea.Id, bea.Id)).Status);
        Assert.Empty(await Voters(lab, sessionId));

        Assert.Equal(CommandStatus.Ok, (await lab.Election.SaveAttendanceAsync(north, camAccount.Id, sessionId, cam.Id, true)).Status);
        Assert.Equal(CommandStatus.Ok, (await lab.Election.SaveAttendanceAsync(north, owner.Id, sessionId, ada.Id, true)).Status);
        Assert.Equal(CommandStatus.Refused, (await lab.Election.CastVoteAsync(north, beaAccount.Id, sessionId, bea.Id, bea.Id)).Status);
        Assert.Empty(await Voters(lab, sessionId));
        Assert.Equal(3, (await Present(lab, sessionId)).Count);

        Assert.Equal(CommandStatus.Ok, (await lab.Election.SaveAttendanceAsync(north, owner.Id, sessionId, ada.Id, false)).Status);
        Assert.Equal(CommandStatus.Ok, (await lab.Election.CastVoteAsync(north, beaAccount.Id, sessionId, bea.Id, bea.Id)).Status);
        Assert.Equal(new[] { bea.Id, cam.Id }.OrderBy(id => id.Value), (await Voters(lab, sessionId)).OrderBy(id => id.Value));
        Assert.DoesNotContain(ada.Id, await Voters(lab, sessionId));
        Assert.Equal(CommandStatus.Refused, (await lab.Election.CastVoteAsync(north, camAccount.Id, sessionId, bea.Id, cam.Id)).Status);
        Assert.Equal(bea.Id, await VoteChoice(lab, sessionId, bea.Id));
        Assert.Equal(CommandStatus.Refused, (await lab.Election.CastVoteAsync(north, beaAccount.Id, sessionId, bea.Id, cam.Id)).Status);

        Assert.Equal(CommandStatus.Ok, (await lab.Election.SaveAttendanceAsync(north, owner.Id, sessionId, ada.Id, true)).Status);
        Assert.Contains(ada.Id, await Present(lab, sessionId));
        Assert.DoesNotContain(ada.Id, await Voters(lab, sessionId));
        Assert.Equal(CommandStatus.Refused, (await lab.Election.CastVoteAsync(north, owner.Id, sessionId, ada.Id, bea.Id)).Status);

        Assert.Equal(CommandStatus.Ok, (await lab.Election.SaveAttendanceAsync(north, beaAccount.Id, sessionId, bea.Id, false)).Status);
        Assert.DoesNotContain(bea.Id, await Present(lab, sessionId));
        Assert.Contains(bea.Id, await Voters(lab, sessionId));
        Assert.Equal(bea.Id, await VoteChoice(lab, sessionId, bea.Id));

        var stranger = (await lab.Accounts.RegisterAsync("stranger@example.com", Password)).Account!;
        var attendance = await lab.Db.SessionAttendance.CountAsync(row => row.SessionId == sessionId);
        var votes = await lab.Db.SessionVotes.CountAsync(row => row.SessionId == sessionId);
        Assert.Equal(CommandStatus.NotFound, (await lab.Election.SaveAttendanceAsync(north, stranger.Id, sessionId, bea.Id, false)).Status);
        Assert.Equal(attendance, await lab.Db.SessionAttendance.CountAsync(row => row.SessionId == sessionId));
        Assert.Equal(votes, await lab.Db.SessionVotes.CountAsync(row => row.SessionId == sessionId));

        var stored = JsonSerializer.Serialize(await lab.Db.ActivityRecords.Where(row => row.Attempt == "mark-attendance").ToListAsync());
        Assert.Contains("mark-attendance", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("save-attendance", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("Ada", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("Bea", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("Cam", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("Legacy", stored, StringComparison.Ordinal);

        var east = new GroupId((await lab.Groups.CreateAsync(owner.Id, "East", "Ada East")).Id!.Value);
        var held = new SessionId((await lab.Sessions.CreateAsync(east, owner.Id, [alpha, beta])).Id!.Value);
        for (var number = 2; number <= 9; number++)
        {
            var account = (await lab.Accounts.RegisterAsync($"seat{number}@example.com", Password)).Account!;
            lab.Db.Memberships.Add(MembershipRows.Account(east, account.Id, "Seat " + number, lab.Clock.UtcNow));
        }

        await lab.Db.SaveChangesAsync();
        var seated = await lab.Db.Memberships.Where(member => member.GroupId == east && member.AccountId != null && member.RemovedAt == null).ToListAsync();
        Assert.Equal(9, seated.Count);
        foreach (var member in seated)
            Assert.Equal(CommandStatus.Ok, (await lab.Election.SaveAttendanceAsync(east, member.AccountId!.Value, held, member.Id, true)).Status);
        var voter = seated.Single(member => member.AccountId == owner.Id);
        Assert.Equal(CommandStatus.Refused, (await lab.Election.CastVoteAsync(east, owner.Id, held, voter.Id, voter.Id)).Status);
        Assert.Empty(await Voters(lab, held));
    }

    private static async Task<Dictionary<string, Guid>> Publish(QuorumLab lab)
    {
        var root = RoadmapFiles.WriteSessionCatalog(Path.Combine(Path.GetTempPath(), "quorum-own-" + Guid.NewGuid().ToString("N")));
        try
        {
            Assert.Equal(0, await lab.Publish.RunAsync(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        return await lab.Db.Topics.ToDictionaryAsync(topic => topic.Key, topic => topic.Id);
    }

    private static Task<List<MemberId>> Voters(QuorumLab lab, SessionId sessionId) =>
        lab.Db.SessionVoters.Where(row => row.SessionId == sessionId).Select(row => row.MembershipId).ToListAsync();

    private static Task<List<MemberId>> Present(QuorumLab lab, SessionId sessionId) =>
        lab.Db.SessionAttendance.Where(row => row.SessionId == sessionId).Select(row => row.MembershipId).ToListAsync();

    private static async Task<MemberId?> VoteChoice(QuorumLab lab, SessionId sessionId, MemberId voterId)
    {
        var vote = await lab.Db.SessionVotes.SingleOrDefaultAsync(row => row.SessionId == sessionId && row.VoterMembershipId == voterId);
        return vote?.CandidateMembershipId;
    }
}
