using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Quorum.App.Features.Groups;
using Quorum.App.Features.Sessions;
using Quorum.App.Features.Shared;
using Quorum.App.SharedKernel;
using Quorum.Tests.Support;

namespace Quorum.Groups.Tests;

public class LeaveRemoveTests : IAsyncLifetime
{
    private const string Password = "correct-horse";

    private PostgresDatabase _database = null!;

    public async Task InitializeAsync() => _database = await PostgresDatabase.StartAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Leaving_matches_removal_and_the_past_session_stays()
    {
        await using var lab = QuorumLab.Open(_database.ConnectionString);
        var topics = await Publish(lab);
        var owner = (await lab.Accounts.RegisterAsync("ada@example.com", Password)).Account!;
        var north = new GroupId((await lab.Groups.CreateAsync(owner.Id, "North", "Ada")).Id!.Value);
        var ada = await Member(lab, owner.Id, north);
        var beaAccount = (await lab.Accounts.RegisterAsync("bea@example.com", Password)).Account!;
        var bea = MembershipRows.Account(north, beaAccount.Id, "Bea", lab.Clock.UtcNow);
        var calAccount = (await lab.Accounts.RegisterAsync("cal@example.com", Password)).Account!;
        var cal = MembershipRows.Account(north, calAccount.Id, "Cal", lab.Clock.UtcNow);
        lab.Db.Memberships.AddRange(bea, cal);
        await lab.Db.SaveChangesAsync();

        var created = await lab.Sessions.CreateAsync(north, owner.Id, [topics[0], topics[1]]);
        Assert.Equal(CommandStatus.Ok, created.Status);
        var session = new SessionId(created.Id!.Value);
        await Mark(lab, north, owner.Id, session, ada);
        await Mark(lab, north, beaAccount.Id, session, bea.Id);
        Assert.Equal(CommandStatus.Ok, (await lab.Election.CastVoteAsync(north, owner.Id, session, ada, bea.Id)).Status);
        lab.Ties.Next = bea.Id;
        Assert.Equal(CommandStatus.Ok, (await lab.Election.CastVoteAsync(north, beaAccount.Id, session, bea.Id, ada)).Status);
        Assert.Equal(bea.Id, await Facilitator(lab, session));

        var attendance = await Ids(lab, session);
        var ballots = await Ballots(lab, session);
        var topicIds = await lab.Db.SessionTopics.Where(link => link.SessionId == session).Select(link => link.TopicId).ToListAsync();

        Assert.Equal(CommandStatus.NotFound, (await lab.Roster.RemoveAsync(north, calAccount.Id, bea.Id)).Status);
        Assert.Null((await lab.Db.Memberships.SingleAsync(member => member.Id == bea.Id)).RemovedAt);
        Assert.Equal(CommandStatus.Ok, (await lab.Roster.RemoveAsync(north, owner.Id, bea.Id)).Status);
        Assert.NotNull((await lab.Db.Memberships.SingleAsync(member => member.Id == bea.Id)).RemovedAt);
        Assert.Equal(attendance, await Ids(lab, session));
        Assert.Equal(ballots, await Ballots(lab, session));
        Assert.Equal(CommandStatus.NotFound, (await lab.Election.SaveAttendanceAsync(north, beaAccount.Id, session, bea.Id, true)).Status);
        Assert.Equal(CommandStatus.NotFound, (await lab.Election.CastVoteAsync(north, beaAccount.Id, session, bea.Id, ada)).Status);
        Assert.Equal(CommandStatus.NotFound, (await lab.Sessions.CreateAsync(north, beaAccount.Id, [topics[0], topics[1]])).Status);

        var later = new SessionId((await lab.Sessions.CreateAsync(north, owner.Id, [topics[0], topics[1]])).Id!.Value);
        Assert.Equal(CommandStatus.NotFound, (await lab.Election.SaveAttendanceAsync(north, beaAccount.Id, later, bea.Id, true)).Status);
        Assert.DoesNotContain(bea.Id, await lab.Db.SessionAttendance.Where(row => row.SessionId == later).Select(row => row.MembershipId).ToListAsync());

        Assert.Equal(CommandStatus.Refused, (await lab.Roster.LeaveAsync(north, owner.Id)).Status);
        Assert.Equal(CommandStatus.Refused, (await lab.Roster.RemoveAsync(north, owner.Id, ada)).Status);
        Assert.Null((await lab.Db.Memberships.SingleAsync(member => member.Id == ada)).RemovedAt);

        Assert.Equal(CommandStatus.Ok, (await lab.Roster.LeaveAsync(north, calAccount.Id)).Status);
        Assert.NotNull((await lab.Db.Memberships.SingleAsync(member => member.Id == cal.Id)).RemovedAt);
        Assert.Equal(CommandStatus.NotFound, (await lab.Election.SaveAttendanceAsync(north, calAccount.Id, session, cal.Id, true)).Status);
        var left = await lab.Db.ActivityRecords.SingleAsync(row => row.Attempt == "leave-group" && row.AccountId == calAccount.Id.Value && row.Outcome == "succeeded");
        var stored = JsonSerializer.Serialize(left);
        Assert.DoesNotContain("cal@example.com", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("Cal", stored, StringComparison.Ordinal);

        var invited = await lab.Invitations.InviteAsync(north, owner.Id, "bea@example.com");
        Assert.Equal(CommandStatus.Ok, invited.Status);
        Assert.Equal(CommandStatus.Ok, (await lab.Invitations.AcceptAsync(beaAccount.Id, new InvitationId(invited.Id!.Value), "Bea")).Status);
        var returned = await lab.Db.Memberships.SingleAsync(member => member.AccountId == beaAccount.Id && member.RemovedAt == null);
        Assert.NotEqual(bea.Id, returned.Id);
        Assert.Equal(attendance, await Ids(lab, session));
        Assert.Equal(topicIds, await lab.Db.SessionTopics.Where(link => link.SessionId == session).Select(link => link.TopicId).ToListAsync());
        Assert.Equal(ballots, await Ballots(lab, session));
        Assert.Contains(ballots, ballot => ballot.Voter == bea.Id && ballot.Candidate == ada);
        Assert.NotNull((await lab.Db.Memberships.SingleAsync(member => member.Id == bea.Id)).RemovedAt);
    }

    private static async Task<List<Guid>> Publish(QuorumLab lab)
    {
        var root = RoadmapFiles.WriteSessionCatalog(Path.Combine(Path.GetTempPath(), "quorum-leave-" + Guid.NewGuid().ToString("N")));
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

    private static async Task<MemberId> Member(QuorumLab lab, AccountId accountId, GroupId groupId) =>
        await lab.Db.Memberships.Where(member => member.AccountId == accountId && member.GroupId == groupId).Select(member => member.Id).SingleAsync();

    private static async Task<MemberId?> Facilitator(QuorumLab lab, SessionId sessionId) =>
        await lab.Db.Sessions.Where(row => row.Id == sessionId).Select(row => row.FacilitatorMembershipId).SingleAsync();

    private static Task<List<MemberId>> Ids(QuorumLab lab, SessionId sessionId) =>
        lab.Db.SessionAttendance.Where(row => row.SessionId == sessionId).Select(row => row.MembershipId).OrderBy(id => id).ToListAsync();

    private static async Task<List<(MemberId Voter, MemberId Candidate)>> Ballots(QuorumLab lab, SessionId sessionId)
    {
        var rows = await lab.Db.SessionVotes.Where(row => row.SessionId == sessionId).ToListAsync();
        return rows.Select(row => (Voter: row.VoterMembershipId, Candidate: row.CandidateMembershipId)).OrderBy(row => row.Voter.Value).ToList();
    }
}
