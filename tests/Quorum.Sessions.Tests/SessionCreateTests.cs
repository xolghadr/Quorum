using Microsoft.EntityFrameworkCore;
using Quorum.App.Features.Groups;
using Quorum.App.Features.Sessions;
using Quorum.App.Features.Shared;
using Quorum.App.SharedKernel;
using Quorum.Tests.Support;

namespace Quorum.Sessions.Tests;

public class SessionCreateTests : IAsyncLifetime
{
    private const string Password = "correct-horse";

    private PostgresDatabase _database = null!;

    public async Task InitializeAsync() => _database = await PostgresDatabase.StartAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task The_creator_or_the_latest_facilitator_creates_a_session_with_its_topics()
    {
        await using var lab = QuorumLab.Open(_database.ConnectionString);
        var topics = await Publish(lab);
        var alpha = topics["01-alpha/alpha-signal-topic"];
        var beta = topics["01-alpha/beta-signal-topic"];
        var gamma = topics["02-gamma/gamma-signal-topic"];
        var design = await lab.Db.WorkedDesigns.Select(row => row.Id).SingleAsync();
        var owner = (await lab.Accounts.RegisterAsync("ada@example.com", Password)).Account!;
        var north = new GroupId((await lab.Groups.CreateAsync(owner.Id, "North", "Ada")).Id!.Value);
        var gusAccount = (await lab.Accounts.RegisterAsync("gus@example.com", Password)).Account!;
        var gus = MembershipRows.Account(north, gusAccount.Id, "Gus", lab.Clock.UtcNow);
        var beaAccount = (await lab.Accounts.RegisterAsync("bea@example.com", Password)).Account!;
        var bea = MembershipRows.Account(north, beaAccount.Id, "Bea", lab.Clock.UtcNow);
        var camAccount = (await lab.Accounts.RegisterAsync("cam@example.com", Password)).Account!;
        var cam = MembershipRows.Account(north, camAccount.Id, "Cam", lab.Clock.UtcNow);
        lab.Db.Memberships.AddRange(gus, bea, cam);
        await lab.Db.SaveChangesAsync();

        Assert.Equal(CommandStatus.Refused, (await lab.Sessions.CreateAsync(north, owner.Id, [alpha])).Status);
        Assert.Equal(CommandStatus.Refused, (await lab.Sessions.CreateAsync(north, owner.Id, [alpha, beta, gamma, alpha])).Status);
        Assert.Equal(CommandStatus.Refused, (await lab.Sessions.CreateAsync(north, owner.Id, [alpha, alpha])).Status);
        Assert.Equal(CommandStatus.Refused, (await lab.Sessions.CreateAsync(north, owner.Id, [alpha, design])).Status);
        Assert.Equal(CommandStatus.Refused, (await lab.Sessions.CreateAsync(north, owner.Id, [alpha, Guid.NewGuid()])).Status);
        Assert.Equal(CommandStatus.Refused, (await lab.Sessions.CreateAsync(north, gusAccount.Id, [alpha, beta])).Status);
        Assert.Equal(0, await lab.Db.Sessions.CountAsync(row => row.GroupId == north));
        Assert.Equal(0, await Pick(lab, north, alpha));

        var created = await lab.Sessions.CreateAsync(north, owner.Id, [alpha, gamma]);
        Assert.Equal(CommandStatus.Ok, created.Status);
        var first = new SessionId(created.Id!.Value);
        Assert.Equal(2, await lab.Db.SessionTopics.CountAsync(link => link.SessionId == first));
        Assert.Equal(1, await Pick(lab, north, alpha));
        Assert.Equal(1, await Pick(lab, north, gamma));

        Assert.Equal(CommandStatus.Ok, (await lab.Sessions.SaveTopicsAsync(north, owner.Id, first, [alpha, beta])).Status);
        Assert.Equal(1, await Pick(lab, north, alpha));
        Assert.Equal(1, await Pick(lab, north, beta));
        Assert.Equal(1, await Pick(lab, north, gamma));
        Assert.Equal(CommandStatus.Refused, (await lab.Sessions.SaveTopicsAsync(north, gusAccount.Id, first, [beta, gamma])).Status);
        Assert.Equal([alpha, beta], await TopicIds(lab, first));
        Assert.Equal(CommandStatus.Refused, (await lab.Sessions.CreateAsync(north, owner.Id, [beta, gamma])).Status);
        Assert.Equal(1, await lab.Db.Sessions.CountAsync(row => row.GroupId == north));
        Assert.Equal(1, await Pick(lab, north, beta));

        await Mark(lab, north, owner.Id, first, await Member(lab, owner.Id, north));
        await Mark(lab, north, gusAccount.Id, first, gus.Id);
        Assert.Equal(CommandStatus.Ok, (await lab.Election.CastVoteAsync(north, owner.Id, first, await Member(lab, owner.Id, north), gus.Id)).Status);
        Assert.Equal(CommandStatus.Ok, (await lab.Election.CastVoteAsync(north, gusAccount.Id, first, gus.Id, gus.Id)).Status);
        Assert.Equal(gus.Id, await Facilitator(lab, first));

        var secondResult = await lab.Sessions.CreateAsync(north, gusAccount.Id, [beta, gamma]);
        Assert.Equal(CommandStatus.Ok, secondResult.Status);
        var second = new SessionId(secondResult.Id!.Value);
        Assert.Equal(2, await Pick(lab, north, beta));
        Assert.Equal(2, await Pick(lab, north, gamma));
        Assert.Equal(CommandStatus.Ok, (await lab.Sessions.SaveTopicsAsync(north, gusAccount.Id, second, [alpha, beta])).Status);
        Assert.Equal(2, await Pick(lab, north, alpha));
        Assert.Equal(2, await Pick(lab, north, beta));
        Assert.Equal(2, await Pick(lab, north, gamma));
        Assert.Equal(CommandStatus.Refused, (await lab.Sessions.SaveTopicsAsync(north, owner.Id, second, [alpha, gamma])).Status);
        Assert.Equal([alpha, beta], await TopicIds(lab, second));

        var picks = await Picks(lab, north);
        Assert.Equal(CommandStatus.Refused, (await lab.Sessions.CreateAsync(north, owner.Id, [alpha, gamma])).Status);
        Assert.Equal(CommandStatus.Refused, (await lab.Sessions.CreateAsync(north, gusAccount.Id, [alpha, gamma])).Status);
        Assert.Equal(picks, await Picks(lab, north));
        Assert.Equal(2, await lab.Db.Sessions.CountAsync(row => row.GroupId == north));

        await Mark(lab, north, beaAccount.Id, second, bea.Id);
        await Mark(lab, north, camAccount.Id, second, cam.Id);
        Assert.Equal(CommandStatus.Ok, (await lab.Election.CastVoteAsync(north, beaAccount.Id, second, bea.Id, cam.Id)).Status);
        Assert.Equal(CommandStatus.Ok, (await lab.Election.CastVoteAsync(north, camAccount.Id, second, cam.Id, cam.Id)).Status);
        Assert.Equal(cam.Id, await Facilitator(lab, second));
        var ownerMember = await Member(lab, owner.Id, north);
        Assert.DoesNotContain(await Present(lab, second), id => id == gus.Id || id == ownerMember);
        Assert.Equal([alpha, beta], await TopicIds(lab, second));
        Assert.Equal(CommandStatus.Refused, (await lab.Sessions.SaveTopicsAsync(north, gusAccount.Id, second, [beta, gamma])).Status);
        Assert.Equal([alpha, beta], await TopicIds(lab, second));

        Assert.Equal(CommandStatus.Ok, (await lab.Roster.RemoveAsync(north, owner.Id, gus.Id)).Status);
        Assert.Equal(CommandStatus.NotFound, (await lab.Sessions.CreateAsync(north, gusAccount.Id, [alpha, gamma])).Status);
        var third = await lab.Sessions.CreateAsync(north, owner.Id, [alpha, gamma]);
        Assert.Equal(CommandStatus.Ok, third.Status);
        Assert.Equal(3, await Pick(lab, north, alpha));
        Assert.Equal(3, await Pick(lab, north, gamma));

        var other = (await lab.Accounts.RegisterAsync("other@example.com", Password)).Account!;
        var south = new GroupId((await lab.Groups.CreateAsync(other.Id, "South", "Other")).Id!.Value);
        Assert.Equal(CommandStatus.NotFound, (await lab.Sessions.SaveTopicsAsync(south, other.Id, second, [alpha, beta])).Status);
        Assert.Equal(0, await lab.Db.Sessions.CountAsync(row => row.GroupId == south));
        Assert.Equal(picks[alpha] + 1, await Pick(lab, north, alpha));
    }

    [Fact]
    public async Task Two_creates_submitted_together_store_one_session()
    {
        await using var setup = QuorumLab.Open(_database.ConnectionString);
        var topics = await Publish(setup);
        var alpha = topics["01-alpha/alpha-signal-topic"];
        var beta = topics["01-alpha/beta-signal-topic"];
        var owner = (await setup.Accounts.RegisterAsync("ada@example.com", Password)).Account!;
        var north = new GroupId((await setup.Groups.CreateAsync(owner.Id, "North", "Ada")).Id!.Value);

        await using var left = QuorumLab.Open(_database.ConnectionString);
        await using var right = QuorumLab.Open(_database.ConnectionString);
        var creates = await Task.WhenAll(
            left.Sessions.CreateAsync(north, owner.Id, [alpha, beta]),
            right.Sessions.CreateAsync(north, owner.Id, [alpha, beta]));
        Assert.Equal(1, creates.Count(result => result.Status == CommandStatus.Ok));
        Assert.Equal(1, creates.Count(result => result.Status == CommandStatus.Refused));
        await using var check = QuorumLab.Open(_database.ConnectionString);
        Assert.Equal(1, await check.Db.Sessions.CountAsync(row => row.GroupId == north));
        Assert.Equal(1, await check.Db.TopicProgress.CountAsync(row => row.GroupId == north && row.TopicId == alpha && row.PickCount == 1));
        Assert.Equal(1, await check.Db.TopicProgress.CountAsync(row => row.GroupId == north && row.TopicId == beta && row.PickCount == 1));
    }

    private static async Task Mark(QuorumLab lab, GroupId groupId, AccountId accountId, SessionId sessionId, MemberId memberId) =>
        Assert.Equal(CommandStatus.Ok, (await lab.Election.SaveAttendanceAsync(groupId, accountId, sessionId, memberId, true)).Status);

    private static async Task<MemberId> Member(QuorumLab lab, AccountId accountId, GroupId groupId) =>
        await lab.Db.Memberships.Where(member => member.AccountId == accountId && member.GroupId == groupId).Select(member => member.Id).SingleAsync();

    private static async Task<MemberId?> Facilitator(QuorumLab lab, SessionId sessionId) =>
        await lab.Db.Sessions.Where(row => row.Id == sessionId).Select(row => row.FacilitatorMembershipId).SingleAsync();

    private static Task<List<MemberId>> Present(QuorumLab lab, SessionId sessionId) =>
        lab.Db.SessionAttendance.Where(row => row.SessionId == sessionId).Select(row => row.MembershipId).ToListAsync();

    private static async Task<int> Pick(QuorumLab lab, GroupId groupId, Guid topicId)
    {
        var progress = await lab.Db.TopicProgress.SingleOrDefaultAsync(row => row.GroupId == groupId && row.TopicId == topicId);
        return progress?.PickCount ?? 0;
    }

    private static Task<Dictionary<Guid, int>> Picks(QuorumLab lab, GroupId groupId) =>
        lab.Db.TopicProgress.Where(row => row.GroupId == groupId).ToDictionaryAsync(row => row.TopicId, row => row.PickCount);

    private static Task<List<Guid>> TopicIds(QuorumLab lab, SessionId sessionId) =>
        lab.Db.SessionTopics.Where(link => link.SessionId == sessionId).OrderBy(link => link.Position).Select(link => link.TopicId).ToListAsync();

    private static async Task<Dictionary<string, Guid>> Publish(QuorumLab lab)
    {
        var root = RoadmapFiles.WriteSessionCatalog(Path.Combine(Path.GetTempPath(), "quorum-create-" + Guid.NewGuid().ToString("N")));
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
}
