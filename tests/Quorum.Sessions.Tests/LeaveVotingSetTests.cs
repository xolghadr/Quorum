using Microsoft.EntityFrameworkCore;
using Quorum.App.Features.Groups;
using Quorum.App.Features.Sessions;
using Quorum.App.Features.Shared;
using Quorum.App.SharedKernel;
using Quorum.Tests.Support;

namespace Quorum.Sessions.Tests;

public class LeaveVotingSetTests : IAsyncLifetime
{
    private const string Password = "correct-horse";

    private PostgresDatabase _database = null!;

    public async Task InitializeAsync() => _database = await PostgresDatabase.StartAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task An_unvoted_member_leaves_the_set_and_the_others_can_elect()
    {
        await using var lab = QuorumLab.Open(_database.ConnectionString);
        var topics = await Publish(lab);
        var (north, owner, ada, beaAccount, bea, camAccount, cam) = await Trio(lab);
        var session = await Open(lab, north, owner, topics, ada, beaAccount, bea, camAccount, cam);

        Assert.Equal(CommandStatus.Ok, (await lab.Election.CastVoteAsync(north, owner, session, ada, cam)).Status);
        Assert.Equal(CommandStatus.Ok, (await lab.Roster.RemoveAsync(north, owner, cam)).Status);
        Assert.DoesNotContain(cam, await Voters(lab, session));
        Assert.Equal(cam, await VoteChoice(lab, session, ada));
        Assert.Contains(cam, await Present(lab, session));

        lab.Ties.Next = ada;
        Assert.Equal(CommandStatus.Ok, (await lab.Election.CastVoteAsync(north, beaAccount, session, bea, ada)).Status);
        Assert.Equal(ada, await Facilitator(lab, session));
        Assert.Equal(CommandStatus.NotFound, (await lab.Election.SaveAttendanceAsync(north, camAccount, session, cam, true)).Status);
    }

    [Fact]
    public async Task Two_voters_elect_nobody_when_the_one_who_has_not_voted_leaves()
    {
        await using var lab = QuorumLab.Open(_database.ConnectionString);
        var topics = await Publish(lab);
        var (north, owner, ada, beaAccount, bea, _, _) = await Trio(lab);
        var created = await lab.Sessions.CreateAsync(north, owner, topics);
        var session = new SessionId(created.Id!.Value);
        await Mark(lab, north, owner, session, ada);
        await Mark(lab, north, beaAccount, session, bea);
        Assert.Equal(CommandStatus.Ok, (await lab.Election.CastVoteAsync(north, owner, session, ada, bea)).Status);
        Assert.Equal(CommandStatus.Ok, (await lab.Roster.LeaveAsync(north, beaAccount)).Status);

        Assert.Null(await Facilitator(lab, session));
        Assert.Equal(bea, await VoteChoice(lab, session, ada));
        Assert.DoesNotContain(bea, await Voters(lab, session));
        Assert.Contains(ada, await Voters(lab, session));
    }

    [Fact]
    public async Task A_member_who_already_voted_stays_in_the_set_after_leaving()
    {
        await using var lab = QuorumLab.Open(_database.ConnectionString);
        var topics = await Publish(lab);
        var (north, owner, ada, beaAccount, bea, _, _) = await Trio(lab);
        var created = await lab.Sessions.CreateAsync(north, owner, topics);
        var session = new SessionId(created.Id!.Value);
        await Mark(lab, north, owner, session, ada);
        await Mark(lab, north, beaAccount, session, bea);
        Assert.Equal(CommandStatus.Ok, (await lab.Election.CastVoteAsync(north, owner, session, ada, bea)).Status);
        Assert.Equal(CommandStatus.Ok, (await lab.Election.CastVoteAsync(north, beaAccount, session, bea, bea)).Status);
        Assert.Equal(bea, await Facilitator(lab, session));
        Assert.Equal(CommandStatus.Ok, (await lab.Election.SaveAttendanceAsync(north, beaAccount, session, bea, false)).Status);
        Assert.Contains(bea, await Voters(lab, session));
        Assert.Equal(bea, await VoteChoice(lab, session, bea));

        Assert.Equal(CommandStatus.Ok, (await lab.Roster.LeaveAsync(north, beaAccount)).Status);
        Assert.Contains(bea, await Voters(lab, session));
        Assert.Equal(bea, await VoteChoice(lab, session, bea));
        Assert.Equal(bea, await Facilitator(lab, session));
        Assert.Equal(CommandStatus.NotFound, (await lab.Election.SaveAttendanceAsync(north, beaAccount, session, bea, true)).Status);
        Assert.Equal(CommandStatus.NotFound, (await lab.Election.CastVoteAsync(north, beaAccount, session, bea, ada)).Status);
    }

    [Fact]
    public async Task Leave_before_the_first_vote_drops_that_attendance_row()
    {
        await using var lab = QuorumLab.Open(_database.ConnectionString);
        var topics = await Publish(lab);
        var (north, owner, ada, beaAccount, bea, _, _) = await Trio(lab);
        var created = await lab.Sessions.CreateAsync(north, owner, topics);
        var session = new SessionId(created.Id!.Value);
        await Mark(lab, north, owner, session, ada);
        await Mark(lab, north, beaAccount, session, bea);
        Assert.Equal(CommandStatus.Ok, (await lab.Roster.LeaveAsync(north, beaAccount)).Status);
        Assert.DoesNotContain(bea, await Present(lab, session));
        Assert.Contains(ada, await Present(lab, session));
        Assert.Empty(await Voters(lab, session));
    }

    [Fact]
    public async Task Votes_for_a_departed_member_can_still_elect_them()
    {
        await using var lab = QuorumLab.Open(_database.ConnectionString);
        var topics = await Publish(lab);
        var (north, owner, ada, beaAccount, bea, camAccount, cam) = await Trio(lab);
        var session = await Open(lab, north, owner, topics, ada, beaAccount, bea, camAccount, cam);
        Assert.Equal(CommandStatus.Ok, (await lab.Election.CastVoteAsync(north, owner, session, ada, cam)).Status);
        Assert.Equal(CommandStatus.Ok, (await lab.Election.CastVoteAsync(north, beaAccount, session, bea, cam)).Status);
        Assert.Equal(CommandStatus.Ok, (await lab.Roster.LeaveAsync(north, camAccount)).Status);

        Assert.Equal(cam, await Facilitator(lab, session));
        Assert.DoesNotContain(cam, await Voters(lab, session));
        Assert.Equal(cam, await VoteChoice(lab, session, ada));
        Assert.Equal(cam, await VoteChoice(lab, session, bea));
        Assert.Equal(CommandStatus.NotFound, (await lab.Sessions.CreateAsync(north, camAccount, topics)).Status);
        var next = await lab.Sessions.CreateAsync(north, owner, topics);
        Assert.Equal(CommandStatus.Ok, next.Status);
        Assert.Equal(2, await lab.Db.Sessions.CountAsync(row => row.GroupId == north));
    }

    private static async Task<SessionId> Open(
        QuorumLab lab,
        GroupId groupId,
        AccountId ownerId,
        IReadOnlyList<Guid> topics,
        MemberId ada,
        AccountId beaAccount,
        MemberId bea,
        AccountId camAccount,
        MemberId cam)
    {
        var created = await lab.Sessions.CreateAsync(groupId, ownerId, topics);
        Assert.Equal(CommandStatus.Ok, created.Status);
        var session = new SessionId(created.Id!.Value);
        await Mark(lab, groupId, ownerId, session, ada);
        await Mark(lab, groupId, beaAccount, session, bea);
        await Mark(lab, groupId, camAccount, session, cam);
        return session;
    }

    private static async Task<(GroupId Group, AccountId Owner, MemberId Ada, AccountId BeaAccount, MemberId Bea, AccountId CamAccount, MemberId Cam)> Trio(QuorumLab lab)
    {
        var creator = (await lab.Accounts.RegisterAsync("ada@example.com", Password)).Account!;
        var north = new GroupId((await lab.Groups.CreateAsync(creator.Id, "North", "Ada")).Id!.Value);
        var ada = await lab.Db.Memberships.Where(member => member.AccountId == creator.Id).Select(member => member.Id).SingleAsync();
        var beaPerson = (await lab.Accounts.RegisterAsync("bea@example.com", Password)).Account!;
        var bea = MembershipRows.Account(north, beaPerson.Id, "Bea", lab.Clock.UtcNow);
        var camPerson = (await lab.Accounts.RegisterAsync("cam@example.com", Password)).Account!;
        var cam = MembershipRows.Account(north, camPerson.Id, "Cam", lab.Clock.UtcNow);
        lab.Db.Memberships.AddRange(bea, cam);
        await lab.Db.SaveChangesAsync();
        return (north, creator.Id, ada, beaPerson.Id, bea.Id, camPerson.Id, cam.Id);
    }

    private static async Task<IReadOnlyList<Guid>> Publish(QuorumLab lab)
    {
        var root = RoadmapFiles.WriteSessionCatalog(Path.Combine(Path.GetTempPath(), "quorum-voters-" + Guid.NewGuid().ToString("N")));
        try
        {
            Assert.Equal(0, await lab.Publish.RunAsync(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        return await lab.Db.Topics.OrderBy(topic => topic.Key).Select(topic => topic.Id).Take(2).ToListAsync();
    }

    private static async Task Mark(QuorumLab lab, GroupId groupId, AccountId accountId, SessionId sessionId, MemberId memberId) =>
        Assert.Equal(CommandStatus.Ok, (await lab.Election.SaveAttendanceAsync(groupId, accountId, sessionId, memberId, true)).Status);

    private static async Task<MemberId?> Facilitator(QuorumLab lab, SessionId sessionId) =>
        await lab.Db.Sessions.Where(row => row.Id == sessionId).Select(row => row.FacilitatorMembershipId).SingleAsync();

    private static Task<List<MemberId>> Voters(QuorumLab lab, SessionId sessionId) =>
        lab.Db.SessionVoters.Where(row => row.SessionId == sessionId).Select(row => row.MembershipId).ToListAsync();

    private static Task<List<MemberId>> Present(QuorumLab lab, SessionId sessionId) =>
        lab.Db.SessionAttendance.Where(row => row.SessionId == sessionId).Select(row => row.MembershipId).ToListAsync();

    private static async Task<MemberId> VoteChoice(QuorumLab lab, SessionId sessionId, MemberId voter) =>
        await lab.Db.SessionVotes.Where(row => row.SessionId == sessionId && row.VoterMembershipId == voter).Select(row => row.CandidateMembershipId).SingleAsync();
}
