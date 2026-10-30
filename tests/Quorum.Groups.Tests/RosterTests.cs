using Microsoft.EntityFrameworkCore;
using Quorum.App.Features.Groups;
using Quorum.App.Features.Shared;
using Quorum.App.SharedKernel;
using Quorum.Tests.Support;

namespace Quorum.Groups.Tests;

public class RosterTests : IAsyncLifetime
{
    private const string Password = "correct-horse";

    private PostgresDatabase _database = null!;

    public async Task InitializeAsync() => _database = await PostgresDatabase.StartAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Roster_names_stay_unique_per_group_and_a_removed_member_keeps_past_session_rows()
    {
        await using var lab = QuorumLab.Open(_database.ConnectionString);
        var root = RoadmapFiles.WriteSessionCatalog(Path.Combine(Path.GetTempPath(), "quorum-roster-" + Guid.NewGuid().ToString("N")));
        try
        {
            Assert.Equal(0, await lab.Publish.RunAsync(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        var owner = (await lab.Accounts.RegisterAsync("ada@example.com", Password)).Account!;
        var northId = (await lab.Groups.CreateAsync(owner.Id, "North", "Ada")).Id!.Value;
        var north = new GroupId(northId);
        var ada = await lab.Db.Memberships.SingleAsync(member => member.GroupId == north);

        Assert.Equal(CommandStatus.Refused, (await lab.Roster.AddAsync(north, owner.Id, "   ")).Status);
        Assert.Equal(CommandStatus.Refused, (await lab.Roster.AddAsync(north, owner.Id, new string('x', 41))).Status);
        Assert.Equal(1, await ActiveCount(lab, north));

        Assert.Equal(CommandStatus.Ok, (await lab.Roster.AddAsync(north, owner.Id, "  Bea  ")).Status);
        var bea = await lab.Db.Memberships.SingleAsync(member => member.GroupId == north && member.AccountId == null);
        Assert.Equal("Bea", bea.DisplayName);
        Assert.Null(bea.AccountId);
        Assert.Equal(CommandStatus.Refused, (await lab.Roster.AddAsync(north, owner.Id, " bea ")).Status);

        Assert.Equal(CommandStatus.Ok, (await lab.Roster.AddAsync(north, owner.Id, "Cal")).Status);
        var cal = await lab.Db.Memberships.SingleAsync(member => member.DisplayName == "Cal");

        Assert.Equal(CommandStatus.Refused, (await lab.Roster.RenameAsync(north, owner.Id, bea.Id, " cal ")).Status);
        Assert.Equal(CommandStatus.Ok, (await lab.Roster.RenameAsync(north, owner.Id, ada.Id, "  Ada Owner  ")).Status);
        lab.Db.ChangeTracker.Clear();
        ada = await lab.Db.Memberships.SingleAsync(member => member.Id == ada.Id);
        Assert.Equal("Ada Owner", ada.DisplayName);
        Assert.Equal(CommandStatus.Refused, (await lab.Roster.RemoveAsync(north, owner.Id, ada.Id)).Status);
        Assert.Null((await lab.Db.Memberships.SingleAsync(member => member.Id == ada.Id)).RemovedAt);

        var topics = await lab.Db.Topics.OrderBy(topic => topic.Key).Take(2).Select(topic => topic.Id).ToListAsync();
        var created = await lab.Sessions.CreateAsync(north, owner.Id, topics);
        var session = new SessionId(created.Id!.Value);
        Assert.Equal(CommandStatus.Ok, (await lab.Election.SaveAttendanceAsync(north, owner.Id, session, [ada.Id, bea.Id])).Status);
        Assert.Equal(CommandStatus.Ok, (await lab.Election.CastVoteAsync(north, owner.Id, session, ada.Id, bea.Id)).Status);
        var picks = await lab.Db.TopicProgress.Where(progress => progress.GroupId == north).Select(progress => progress.PickCount).ToListAsync();
        var topicIds = await lab.Db.SessionTopics.Where(link => link.SessionId == session).Select(link => link.TopicId).ToListAsync();

        Assert.Equal(CommandStatus.Ok, (await lab.Roster.RemoveAsync(north, owner.Id, bea.Id)).Status);
        lab.Db.ChangeTracker.Clear();
        Assert.NotNull((await lab.Db.Memberships.SingleAsync(member => member.Id == bea.Id)).RemovedAt);
        Assert.DoesNotContain(await ActiveNames(lab, north), name => name == "Bea");
        Assert.Equal(picks.OrderBy(count => count), (await lab.Db.TopicProgress.Where(progress => progress.GroupId == north).Select(progress => progress.PickCount).ToListAsync()).OrderBy(count => count));
        Assert.Equal(topicIds.OrderBy(id => id), (await lab.Db.SessionTopics.Where(link => link.SessionId == session).Select(link => link.TopicId).ToListAsync()).OrderBy(id => id));
        Assert.Contains(await lab.Db.SessionAttendance.Where(row => row.SessionId == session).Select(row => row.MembershipId).ToListAsync(), id => id == bea.Id);
        Assert.Contains(await lab.Db.SessionVoters.Where(row => row.SessionId == session).Select(row => row.MembershipId).ToListAsync(), id => id == bea.Id);

        var later = new SessionId((await lab.Sessions.CreateAsync(north, owner.Id, [])).Id!.Value);
        Assert.Equal(CommandStatus.Refused, (await lab.Election.SaveAttendanceAsync(north, owner.Id, later, [ada.Id, bea.Id])).Status);
        Assert.Empty(await lab.Db.SessionAttendance.Where(row => row.SessionId == later).ToListAsync());
        Assert.Equal(CommandStatus.Ok, (await lab.Election.SaveAttendanceAsync(north, owner.Id, later, [ada.Id, cal.Id])).Status);

        Assert.Equal(CommandStatus.Ok, (await lab.Roster.AddAsync(north, owner.Id, " Bea ")).Status);
        Assert.Equal(3, await ActiveCount(lab, north));

        for (var number = 4; number <= Group.MemberMax; number++)
            Assert.Equal(CommandStatus.Ok, (await lab.Roster.AddAsync(north, owner.Id, "Member " + number)).Status);
        Assert.Equal(CommandStatus.Refused, (await lab.Roster.AddAsync(north, owner.Id, "Ninth")).Status);
        Assert.Equal(Group.MemberMax, await ActiveCount(lab, north));

        var other = (await lab.Accounts.RegisterAsync("other@example.com", Password)).Account!;
        var south = new GroupId((await lab.Groups.CreateAsync(other.Id, "South", "Other")).Id!.Value);
        Assert.Equal(CommandStatus.NotFound, (await lab.Roster.RenameAsync(south, other.Id, cal.Id, "Nope")).Status);
        Assert.Equal(CommandStatus.NotFound, (await lab.Roster.RemoveAsync(south, other.Id, cal.Id)).Status);
        Assert.Equal(CommandStatus.NotFound, (await lab.Roster.RemoveAsync(north, owner.Id, MemberId.New())).Status);
        Assert.Equal(CommandStatus.NotFound, (await lab.Roster.AddAsync(south, owner.Id, "Spy")).Status);
        Assert.Equal("Cal", (await lab.Db.Memberships.SingleAsync(member => member.Id == cal.Id)).DisplayName);

        var topic = await lab.Db.Topics.SingleAsync(row => row.Key == "01-alpha/alpha-signal-topic");
        var section = await lab.Db.TopicSections.SingleAsync(row => row.TopicId == topic.Id);
        Assert.Contains("QUORUM_FIXTURE_SENTENCE_ALPHA", section.Markdown);
        Assert.Equal(owner.Id, (await lab.Db.Groups.SingleAsync(group => group.Id == north)).CreatorAccountId);
        Assert.Null((await lab.Db.Memberships.SingleAsync(member => member.Id == ada.Id)).RemovedAt);
    }

    private static Task<int> ActiveCount(QuorumLab lab, GroupId groupId) =>
        lab.Db.Memberships.CountAsync(member => member.GroupId == groupId && member.RemovedAt == null);

    private static Task<List<string>> ActiveNames(QuorumLab lab, GroupId groupId) =>
        lab.Db.Memberships
            .Where(member => member.GroupId == groupId && member.RemovedAt == null)
            .Select(member => member.DisplayName)
            .ToListAsync();
}
