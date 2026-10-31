using Microsoft.EntityFrameworkCore;
using Quorum.App.Features.Groups;
using Quorum.App.Features.Sessions;
using Quorum.App.Features.Shared;
using Quorum.App.Infrastructure.Persistence;
using Quorum.App.SharedKernel;
using Quorum.Tests.Support;

namespace Quorum.Sessions.Tests;

public class SessionRulesTests : IAsyncLifetime
{
    private const string Password = "correct-horse";

    private PostgresDatabase _database = null!;

    public async Task InitializeAsync() => _database = await PostgresDatabase.StartAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Picks_increase_once_per_new_topic_and_stay_inside_the_group()
    {
        await using var lab = QuorumLab.Open(_database.ConnectionString);
        var owner = (await lab.Accounts.RegisterAsync("ada@example.com", Password)).Account!;
        var north = new GroupId((await lab.Groups.CreateAsync(owner.Id, "North", "Ada")).Id!.Value);

        Assert.Equal(CommandStatus.Refused, (await lab.Sessions.CreateAsync(north, owner.Id, [])).Status);
        Assert.Equal(0, await lab.Db.Sessions.CountAsync());
        Assert.Equal(0, await lab.Db.Topics.CountAsync());

        var topics = await Publish(lab);
        var alpha = topics["01-alpha/alpha-signal-topic"];
        var beta = topics["01-alpha/beta-signal-topic"];
        var gamma = topics["02-gamma/gamma-signal-topic"];
        var design = await lab.Db.WorkedDesigns.Select(row => row.Id).SingleAsync();
        Assert.Equal(3, await lab.Db.Topics.CountAsync());
        Assert.Null(typeof(Session).GetProperty("ScheduledFor"));

        Assert.Equal(CommandStatus.Refused, (await lab.Sessions.CreateAsync(north, owner.Id, [alpha])).Status);
        Assert.Equal(CommandStatus.Refused, (await lab.Sessions.CreateAsync(north, owner.Id, [alpha, beta, gamma, alpha])).Status);
        Assert.Equal(CommandStatus.Refused, (await lab.Sessions.CreateAsync(north, owner.Id, [alpha, alpha])).Status);
        Assert.Equal(CommandStatus.Refused, (await lab.Sessions.CreateAsync(north, owner.Id, [alpha, design])).Status);
        Assert.Equal(CommandStatus.Refused, (await lab.Sessions.CreateAsync(north, owner.Id, [alpha, Guid.NewGuid()])).Status);
        Assert.Equal(0, await lab.Db.Sessions.CountAsync());
        Assert.Equal(0, await lab.Db.TopicProgress.CountAsync());
        Assert.Equal(3, await lab.Db.Topics.CountAsync());

        var gusAccount = (await lab.Accounts.RegisterAsync("gus@example.com", Password)).Account!;
        var gus = new Membership
        {
            Id = MemberId.New(),
            GroupId = north,
            AccountId = gusAccount.Id,
            DisplayName = "Gus",
            DisplayNameKey = "gus",
            CreatedAt = lab.Clock.UtcNow,
        };
        lab.Db.Memberships.Add(gus);
        await lab.Db.SaveChangesAsync();
        Assert.Equal(CommandStatus.Refused, (await lab.Sessions.CreateAsync(north, gusAccount.Id, [alpha, gamma])).Status);

        var other = (await lab.Accounts.RegisterAsync("other@example.com", Password)).Account!;
        var south = new GroupId((await lab.Groups.CreateAsync(other.Id, "South", "Other")).Id!.Value);
        Assert.Equal(CommandStatus.NotFound, (await lab.Sessions.CreateAsync(north, other.Id, [alpha, gamma])).Status);
        Assert.Equal(0, await lab.Db.Sessions.CountAsync());

        var alphaTrack = await lab.Db.Tracks.SingleAsync(track => track.Key == "01-alpha");
        var gammaTrack = await lab.Db.Tracks.SingleAsync(track => track.Key == "02-gamma");
        var emptyTrack = await lab.Db.Tracks.SingleAsync(track => track.Key == "03-empty");
        Assert.False(await SessionCommands.TrackIsReviewed(lab.Db, north, alphaTrack.Id));
        Assert.False(await SessionCommands.TrackIsReviewed(lab.Db, north, emptyTrack.Id));

        var created = await lab.Sessions.CreateAsync(north, owner.Id, [alpha, gamma]);
        Assert.Equal(CommandStatus.Ok, created.Status);
        var first = new SessionId(created.Id!.Value);
        var session = await lab.Db.Sessions.SingleAsync();
        Assert.Equal(1, session.Number);
        Assert.Equal(lab.Clock.Today, session.CreatedOn);
        Assert.Null(session.FacilitatorMembershipId);
        Assert.Empty(await lab.Db.SessionAttendance.Where(row => row.SessionId == first).ToListAsync());
        Assert.Equal(1, await Pick(lab, north, alpha));
        Assert.Equal(0, await Pick(lab, north, beta));
        Assert.Equal(1, await Pick(lab, north, gamma));
        Assert.False(await SessionCommands.TrackIsReviewed(lab.Db, north, alphaTrack.Id));
        Assert.True(await SessionCommands.TrackIsReviewed(lab.Db, north, gammaTrack.Id));
        Assert.Equal([beta, alpha, gamma], (await SessionCommands.ChooserAsync(lab.Db, north)).Select(choice => choice.Id).ToArray());

        Assert.Equal(CommandStatus.Ok, (await lab.Sessions.SaveTopicsAsync(north, owner.Id, first, [alpha, beta])).Status);
        Assert.Equal(1, await Pick(lab, north, alpha));
        Assert.Equal(1, await Pick(lab, north, beta));
        Assert.Equal(1, await Pick(lab, north, gamma));
        Assert.True(await SessionCommands.TrackIsReviewed(lab.Db, north, alphaTrack.Id));
        Assert.Equal([alpha, beta], await TopicIds(lab, first));

        Assert.Equal(CommandStatus.Ok, (await lab.Sessions.SaveTopicsAsync(north, owner.Id, first, [beta, gamma])).Status);
        Assert.Equal(1, await Pick(lab, north, alpha));
        Assert.Equal(1, await Pick(lab, north, beta));
        Assert.Equal(2, await Pick(lab, north, gamma));
        Assert.Equal([beta, gamma], await TopicIds(lab, first));

        Assert.Equal(CommandStatus.Refused, (await lab.Sessions.SaveTopicsAsync(north, gusAccount.Id, first, [alpha, beta])).Status);
        Assert.Equal([beta, gamma], await TopicIds(lab, first));

        var ada = await lab.Db.Memberships.SingleAsync(member => member.AccountId == owner.Id && member.GroupId == north);
        var beaAccount = (await lab.Accounts.RegisterAsync("bea@example.com", Password)).Account!;
        var bea = MembershipRows.Account(north, beaAccount.Id, "Bea", lab.Clock.UtcNow);
        lab.Db.Memberships.Add(bea);
        await lab.Db.SaveChangesAsync();
        await Elect(lab, north, first, gus.Id, ada.Id, gus.Id);
        Assert.Equal(gus.Id, await Facilitator(lab, first));
        Assert.Equal(CommandStatus.Refused, (await lab.Sessions.SaveTopicsAsync(north, owner.Id, first, [alpha, beta])).Status);
        Assert.Equal([beta, gamma], await TopicIds(lab, first));

        var secondResult = await lab.Sessions.CreateAsync(north, gusAccount.Id, [alpha, beta]);
        Assert.Equal(CommandStatus.Ok, secondResult.Status);
        var second = new SessionId(secondResult.Id!.Value);
        var secondRow = await lab.Db.Sessions.SingleAsync(row => row.Id == second);
        Assert.Equal(2, secondRow.Number);
        Assert.Equal([alpha, beta], await TopicIds(lab, second));
        Assert.Empty(await lab.Db.SessionAttendance.Where(row => row.SessionId == second).ToListAsync());
        Assert.Equal(CommandStatus.Refused, (await lab.Sessions.CreateAsync(north, owner.Id, [alpha, gamma])).Status);
        Assert.Equal(2, await Pick(lab, north, alpha));
        Assert.Equal(2, await Pick(lab, north, beta));
        Assert.Equal(2, await Pick(lab, north, gamma));

        await Elect(lab, north, second, bea.Id, ada.Id, bea.Id);
        Assert.Equal(bea.Id, await Facilitator(lab, second));
        Assert.DoesNotContain(await Voters(lab, second), id => id == gus.Id);
        Assert.Equal(CommandStatus.Refused, (await lab.Sessions.SaveTopicsAsync(north, gusAccount.Id, second, [alpha, beta, gamma])).Status);
        Assert.Equal(CommandStatus.Refused, (await lab.Sessions.SaveTopicsAsync(north, owner.Id, second, [alpha, gamma])).Status);
        Assert.Equal([alpha, beta], await TopicIds(lab, second));
        Assert.Equal([alpha, beta, gamma], (await SessionCommands.ChooserAsync(lab.Db, north)).Select(choice => choice.Id).ToArray());

        var picks = await lab.Db.TopicProgress.Where(progress => progress.GroupId == north).ToDictionaryAsync(progress => progress.TopicId, progress => progress.PickCount);
        Assert.Equal(CommandStatus.NotFound, (await lab.Sessions.SaveTopicsAsync(south, other.Id, first, [alpha, beta])).Status);
        Assert.Equal(CommandStatus.NotFound, (await lab.Sessions.SaveTopicsAsync(north, other.Id, first, [alpha, beta])).Status);
        Assert.Equal(CommandStatus.NotFound, (await lab.Sessions.CreateAsync(north, other.Id, [])).Status);
        Assert.Equal(CommandStatus.NotFound, (await lab.Election.SaveAttendanceAsync(north, other.Id, second, ada.Id, true)).Status);
        Assert.Equal(CommandStatus.NotFound, (await lab.Election.CastVoteAsync(north, other.Id, second, ada.Id, bea.Id)).Status);
        lab.Db.ChangeTracker.Clear();
        Assert.Equal(picks, await lab.Db.TopicProgress.Where(progress => progress.GroupId == north).ToDictionaryAsync(progress => progress.TopicId, progress => progress.PickCount));
        Assert.Equal(0, await lab.Db.Sessions.CountAsync(row => row.GroupId == south));
        Assert.Equal(0, await lab.Db.TopicProgress.CountAsync(progress => progress.GroupId == south));

        var thirdGroup = new GroupId((await lab.Groups.CreateAsync(owner.Id, "West", "Ada West")).Id!.Value);
        var three = await lab.Sessions.CreateAsync(thirdGroup, owner.Id, [alpha, beta, gamma]);
        Assert.Equal(CommandStatus.Ok, three.Status);
        Assert.Equal(3, await lab.Db.SessionTopics.CountAsync(link => link.SessionId == new SessionId(three.Id!.Value)));
        Assert.Equal(0, await lab.Db.SessionAttendance.CountAsync(row => row.SessionId == new SessionId(three.Id!.Value)));
    }

    [Fact]
    public async Task Votes_freeze_the_voting_set_and_a_tie_keeps_the_replacement_choice()
    {
        await using var lab = QuorumLab.Open(_database.ConnectionString);
        var topics = await Publish(lab);
        var alpha = topics["01-alpha/alpha-signal-topic"];
        var beta = topics["01-alpha/beta-signal-topic"];
        var owner = (await lab.Accounts.RegisterAsync("ada@example.com", Password)).Account!;
        var north = new GroupId((await lab.Groups.CreateAsync(owner.Id, "North", "Ada")).Id!.Value);
        var beaAccount = (await lab.Accounts.RegisterAsync("bea@example.com", Password)).Account!;
        var calAccount = (await lab.Accounts.RegisterAsync("cal@example.com", Password)).Account!;
        lab.Db.Memberships.Add(MembershipRows.Account(north, beaAccount.Id, "Bea", lab.Clock.UtcNow));
        lab.Db.Memberships.Add(MembershipRows.Account(north, calAccount.Id, "Cal", lab.Clock.UtcNow));
        await lab.Db.SaveChangesAsync();
        var members = await lab.Db.Memberships.Where(member => member.GroupId == north).ToDictionaryAsync(member => member.DisplayName, member => member.Id);
        var ada = members["Ada"];
        var bea = members["Bea"];
        var cal = members["Cal"];

        var created = await lab.Sessions.CreateAsync(north, owner.Id, [alpha, beta]);
        var first = new SessionId(created.Id!.Value);
        Assert.Equal(CommandStatus.Ok, (await lab.Election.SaveAttendanceAsync(north, owner.Id, first, ada, true)).Status);
        Assert.Equal(CommandStatus.Ok, (await lab.Election.SaveAttendanceAsync(north, owner.Id, first, ada, true)).Status);
        Assert.Equal([ada], await Present(lab, first));
        Assert.Equal(CommandStatus.Refused, (await lab.Election.SaveAttendanceAsync(north, owner.Id, first, MemberId.New(), true)).Status);
        Assert.Equal(CommandStatus.Refused, (await lab.Election.SaveAttendanceAsync(north, owner.Id, first, bea, true)).Status);
        Assert.Equal([ada], await Present(lab, first));
        Assert.Equal(CommandStatus.Refused, (await lab.Election.CastVoteAsync(north, owner.Id, first, ada, ada)).Status);
        Assert.Empty(await Voters(lab, first));

        Assert.Equal(CommandStatus.Ok, (await lab.Election.SaveAttendanceAsync(north, beaAccount.Id, first, bea, true)).Status);
        Assert.Equal(CommandStatus.Ok, (await lab.Election.SaveAttendanceAsync(north, calAccount.Id, first, cal, true)).Status);
        Assert.Equal(CommandStatus.Refused, (await lab.Election.CastVoteAsync(north, owner.Id, first, ada, ada)).Status);
        Assert.Empty(await Voters(lab, first));
        Assert.Equal(3, (await Present(lab, first)).Count);

        Assert.Equal(CommandStatus.Ok, (await lab.Election.SaveAttendanceAsync(north, calAccount.Id, first, cal, false)).Status);
        Assert.Equal(CommandStatus.Ok, (await lab.Election.CastVoteAsync(north, owner.Id, first, ada, ada)).Status);
        Assert.Equal(new[] { ada, bea }.OrderBy(id => id.Value), (await Voters(lab, first)).OrderBy(id => id.Value));
        Assert.Equal(ada, await VoteChoice(lab, first, ada));

        Assert.Equal(CommandStatus.Ok, (await lab.Election.SaveAttendanceAsync(north, calAccount.Id, first, cal, true)).Status);
        Assert.Contains(cal, await Present(lab, first));
        Assert.DoesNotContain(cal, await Voters(lab, first));
        Assert.Equal(CommandStatus.Refused, (await lab.Election.CastVoteAsync(north, calAccount.Id, first, cal, bea)).Status);
        Assert.Equal(CommandStatus.Refused, (await lab.Election.CastVoteAsync(north, beaAccount.Id, first, bea, cal)).Status);
        Assert.Equal(CommandStatus.Refused, (await lab.Election.CastVoteAsync(north, owner.Id, first, ada, bea)).Status);
        Assert.Equal(ada, await VoteChoice(lab, first, ada));
        Assert.Null(await Facilitator(lab, first));

        Assert.Equal(CommandStatus.Ok, (await lab.Election.SaveAttendanceAsync(north, owner.Id, first, ada, false)).Status);
        Assert.Equal(new[] { bea, cal }.OrderBy(id => id.Value), (await Present(lab, first)).OrderBy(id => id.Value));
        Assert.Equal(ada, await VoteChoice(lab, first, ada));
        Assert.Contains(ada, await Voters(lab, first));

        lab.Ties.Next = ada;
        Assert.Equal(CommandStatus.Ok, (await lab.Election.CastVoteAsync(north, beaAccount.Id, first, bea, bea)).Status);
        Assert.Equal(ada, await Facilitator(lab, first));
        Assert.Equal(CommandStatus.Refused, (await lab.Election.CastVoteAsync(north, beaAccount.Id, first, bea, ada)).Status);

        var second = new SessionId((await lab.Sessions.CreateAsync(north, owner.Id, [alpha, beta])).Id!.Value);
        Assert.Equal(CommandStatus.Ok, (await lab.Election.SaveAttendanceAsync(north, beaAccount.Id, second, bea, true)).Status);
        Assert.Equal(CommandStatus.Ok, (await lab.Election.SaveAttendanceAsync(north, calAccount.Id, second, cal, true)).Status);
        Assert.Equal(CommandStatus.Ok, (await lab.Election.CastVoteAsync(north, beaAccount.Id, second, bea, cal)).Status);
        Assert.Null(await Facilitator(lab, second));
        Assert.Equal(CommandStatus.Ok, (await lab.Election.CastVoteAsync(north, calAccount.Id, second, cal, cal)).Status);
        Assert.Equal(cal, await Facilitator(lab, second));
        Assert.DoesNotContain(ada, await Voters(lab, second));
        Assert.Equal(CommandStatus.Refused, (await lab.Sessions.SaveTopicsAsync(north, owner.Id, second, [alpha, beta])).Status);
        Assert.Equal([alpha, beta], await TopicIds(lab, second));
        Assert.Equal(2, await Pick(lab, north, alpha));
        Assert.Equal(2, await Pick(lab, north, beta));

        Assert.Equal(CommandStatus.Ok, (await lab.Election.SaveAttendanceAsync(north, beaAccount.Id, second, bea, false)).Status);
        Assert.Equal(CommandStatus.Ok, (await lab.Election.SaveAttendanceAsync(north, calAccount.Id, second, cal, false)).Status);
        Assert.Empty(await Present(lab, second));
        Assert.Equal(2, (await Voters(lab, second)).Count);

        for (var number = 4; number <= Group.MemberMax; number++)
            lab.Db.Memberships.Add(MembershipRows.NameOnly(north, "Member " + number, lab.Clock.UtcNow));
        await lab.Db.SaveChangesAsync();
        Assert.Equal(CommandStatus.Refused, (await lab.Roster.AddAsync(north, owner.Id, "Member 9")).Status);
        var everyone = await lab.Db.Memberships.Where(member => member.GroupId == north && member.RemovedAt == null).Select(member => member.Id).ToListAsync();
        Assert.Equal(Group.MemberMax, everyone.Count);
        var third = new SessionId((await lab.Sessions.CreateAsync(north, owner.Id, [alpha, beta])).Id!.Value);
        var legacy = await lab.Db.Memberships.SingleAsync(member => member.GroupId == north && member.AccountId == null && member.DisplayName == "Member 4");
        Assert.Equal(CommandStatus.Refused, (await lab.Election.SaveAttendanceAsync(north, owner.Id, third, legacy.Id, true)).Status);
        Assert.Empty(await Present(lab, third));
        Assert.Equal(CommandStatus.Ok, (await lab.Election.SaveAttendanceAsync(north, owner.Id, third, ada, true)).Status);
        Assert.Equal(CommandStatus.Ok, (await lab.Election.SaveAttendanceAsync(north, beaAccount.Id, third, bea, true)).Status);
        Assert.Equal(CommandStatus.Ok, (await lab.Election.SaveAttendanceAsync(north, calAccount.Id, third, cal, true)).Status);
        Assert.Equal(3, (await Present(lab, third)).Count);

        var choice = new EqualChanceTieBreak().Choose([ada, bea]);
        Assert.Contains(choice, new[] { ada, bea });
    }

    private static async Task<Dictionary<string, Guid>> Publish(QuorumLab lab)
    {
        var root = RoadmapFiles.WriteSessionCatalog(Path.Combine(Path.GetTempPath(), "quorum-sessions-" + Guid.NewGuid().ToString("N")));
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

    private static async Task Elect(
        QuorumLab lab,
        GroupId groupId,
        SessionId sessionId,
        MemberId winner,
        params MemberId[] present)
    {
        var accounts = await lab.Db.Memberships
            .Where(member => present.Contains(member.Id))
            .ToDictionaryAsync(member => member.Id, member => member.AccountId);
        foreach (var memberId in present)
        {
            var accountId = accounts[memberId];
            Assert.NotNull(accountId);
            Assert.Equal(CommandStatus.Ok, (await lab.Election.SaveAttendanceAsync(groupId, accountId.Value, sessionId, memberId, true)).Status);
        }

        foreach (var voter in present)
        {
            var candidate = present.Length >= 3 && voter == winner ? present.First(id => id != voter) : winner;
            Assert.Equal(CommandStatus.Ok, (await lab.Election.CastVoteAsync(groupId, accounts[voter]!.Value, sessionId, voter, candidate)).Status);
        }

        Assert.Equal(winner, await Facilitator(lab, sessionId));
    }

    private static async Task<int> Pick(QuorumLab lab, GroupId groupId, Guid topicId)
    {
        var progress = await lab.Db.TopicProgress.SingleOrDefaultAsync(row => row.GroupId == groupId && row.TopicId == topicId);
        return progress?.PickCount ?? 0;
    }

    private static Task<List<Guid>> TopicIds(QuorumLab lab, SessionId sessionId) =>
        lab.Db.SessionTopics
            .Where(link => link.SessionId == sessionId)
            .OrderBy(link => link.Position)
            .Select(link => link.TopicId)
            .ToListAsync();

    private static async Task<MemberId?> Facilitator(QuorumLab lab, SessionId sessionId)
    {
        await using var fresh = lab.Db.Database.GetConnectionString() is { } connection
            ? QuorumDbContext.Create(connection)
            : throw new InvalidOperationException("The test database has no connection string.");
        return await fresh.Sessions.Where(row => row.Id == sessionId).Select(row => row.FacilitatorMembershipId).SingleAsync();
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
