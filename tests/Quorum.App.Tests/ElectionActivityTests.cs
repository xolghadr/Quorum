using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Quorum.App.Features.Accounts;
using Quorum.App.Features.Groups;
using Quorum.App.Features.Shared;
using Quorum.App.Infrastructure.Logging;
using Quorum.App.Infrastructure.Persistence;
using Quorum.App.SharedKernel;
using Quorum.Tests.Support;

namespace Quorum.App.Tests;

public sealed class ElectionActivityTests : IAsyncLifetime
{
    private const string Password = "correct-horse";
    private static readonly Regex ReferencePattern = new("^[23456789ABCDEFGHJKMNPQRSTVWXYZ]{8}$", RegexOptions.CultureInvariant);

    private PostgresDatabase _database = null!;

    public async Task InitializeAsync() => _database = await PostgresDatabase.StartAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Electing_vote_shares_one_reference_and_set_topics_uses_that_name()
    {
        await _database.ResetAsync();
        await using var lab = QuorumLab.Open(_database.ConnectionString);
        var owner = (await lab.Accounts.RegisterAsync("ada@example.com", Password)).Account!;
        var groupId = new GroupId((await lab.Groups.CreateAsync(owner.Id, "North", "Ada")).Id!.Value);
        Assert.Equal(CommandStatus.Ok, (await lab.Roster.AddAsync(groupId, owner.Id, "Bea")).Status);
        var root = RoadmapFiles.WriteFixture(Path.Combine(Path.GetTempPath(), "quorum-elect-" + Guid.NewGuid().ToString("N")));
        try
        {
            Assert.Equal(0, await lab.Publish.RunAsync(root));
            var topicIds = await lab.Db.Topics.OrderBy(topic => topic.Key).Select(topic => topic.Id).Take(2).ToListAsync();
            var created = await lab.Sessions.CreateAsync(groupId, owner.Id, topicIds);
            var sessionId = new SessionId(created.Id!.Value);
            Assert.Equal(CommandStatus.Ok, (await lab.Sessions.SaveTopicsAsync(groupId, owner.Id, sessionId, topicIds)).Status);
            var topics = await lab.Db.ActivityRecords.SingleAsync(row => row.Attempt == "set-topics");
            Assert.Equal(ActivityRecord.Succeeded, topics.Outcome);
            Assert.DoesNotContain(await lab.Db.ActivityRecords.Select(row => row.Attempt).ToListAsync(), attempt => attempt == "save-topics");

            var members = await lab.Db.Memberships.Where(member => member.GroupId == groupId).Select(member => member.Id).ToListAsync();
            Assert.Equal(CommandStatus.Ok, (await lab.Election.SaveAttendanceAsync(groupId, owner.Id, sessionId, members)).Status);
            Assert.Equal(CommandStatus.Ok, (await lab.Election.CastVoteAsync(groupId, owner.Id, sessionId, members[0], members[0])).Status);
            Assert.Equal(CommandStatus.Ok, (await lab.Election.CastVoteAsync(groupId, owner.Id, sessionId, members[1], members[0])).Status);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        var election = await lab.Db.ActivityRecords.SingleAsync(row => row.Attempt == "elect-facilitator");
        var pair = await lab.Db.ActivityRecords.Where(row => row.Reference == election.Reference).OrderBy(row => row.Attempt).ToListAsync();
        Assert.Equal(["cast-vote", "elect-facilitator"], pair.Select(row => row.Attempt).ToArray());
        Assert.All(pair, row => Assert.Equal(ActivityRecord.Succeeded, row.Outcome));
        Assert.Equal(2, await lab.Db.ActivityRecords.CountAsync(row => row.Reference == election.Reference));

        var writer = new StringWriter();
        Assert.Equal(0, ActivityCommand.Execute(lab.Provider, election.Reference, writer));
        var printed = writer.ToString();
        var castAt = printed.IndexOf("attempt: cast-vote", StringComparison.Ordinal);
        var electAt = printed.IndexOf("attempt: elect-facilitator", StringComparison.Ordinal);
        Assert.True(castAt >= 0 && electAt > castAt);
        Assert.DoesNotContain(Password, printed);
        Assert.DoesNotContain("@", printed);
        Assert.Matches(ReferencePattern, election.Reference);
        Assert.Equal("23456789ABCDEFGHJKMNPQRSTVWXYZ", ActivityReference.Alphabet);
    }

    [Fact]
    public async Task Self_vote_page_shows_the_refused_reference()
    {
        await _database.ResetAsync();
        var root = RoadmapFiles.WriteFixture(Path.Combine(Path.GetTempPath(), "quorum-self-" + Guid.NewGuid().ToString("N")));
        try
        {
            await using (var lab = QuorumLab.Open(_database.ConnectionString))
                Assert.Equal(0, await lab.Publish.RunAsync(root));

            await using var app = new ObservabilityApp(_database.ConnectionString);
            using var ada = Browser(app);
            await Register(ada, "ada@example.com");
            await CreateGroup(ada, "North", "Ada");
            await AddMember(ada, "Bea");
            await AddMember(ada, "Gus");
            using var other = Browser(app);
            await Register(other, "other@example.com");
            await CreateGroup(other, "South", "Other");

            var sessions = await ada.GetStringAsync("/sessions");
            var topicIds = Regex.Matches(sessions, "value=\"([0-9a-fA-F-]{36})\"")
                .Select(match => match.Groups[1].Value)
                .Distinct()
                .Take(2)
                .ToArray();
            Assert.Equal(2, topicIds.Length);
            var created = await Post(ada, "/sessions", "/sessions",
            [
                new("Topic1", topicIds[0]),
                new("Topic2", topicIds[1]),
            ]);
            var sessionPath = RedirectPath(created);
            await using var db = _database.CreateContext();
            var members = await db.Memberships
                .Where(member => member.DisplayName == "Ada" || member.DisplayName == "Bea" || member.DisplayName == "Gus")
                .Select(member => member.Id.Value)
                .ToListAsync();
            Assert.Equal(3, members.Count);
            var sessionId = Guid.Parse(sessionPath.Split('/')[2]);
            await Post(ada, sessionPath, $"/sessions/{sessionId}/attendance", members.Select(id => new KeyValuePair<string, string>("members", id.ToString())).ToList());
            var adaId = await db.Memberships.Where(member => member.DisplayName == "Ada").Select(member => member.Id.Value).SingleAsync();
            var voted = await Post(ada, sessionPath, $"/sessions/{sessionId}/votes",
            [
                new("voterId", adaId.ToString()),
                new("candidateId", adaId.ToString()),
            ]);
            var refusedPath = RedirectPath(voted);
            var reference = Regex.Match(refusedPath, "reference=([23456789ABCDEFGHJKMNPQRSTVWXYZ]{8})").Groups[1].Value;
            Assert.Matches(ReferencePattern, reference);
            var page = await ada.GetStringAsync(refusedPath);
            Assert.Contains("That change was not saved.", page, StringComparison.Ordinal);
            Assert.Contains(reference, page, StringComparison.Ordinal);
            Assert.DoesNotContain("South", page, StringComparison.Ordinal);
            Assert.DoesNotContain("at Quorum", page, StringComparison.Ordinal);
            Assert.DoesNotContain(Password, page, StringComparison.Ordinal);

            var rows = await db.ActivityRecords.Where(row => row.Reference == reference).ToListAsync();
            var vote = Assert.Single(rows);
            Assert.Equal("cast-vote", vote.Attempt);
            Assert.Equal(ActivityRecord.Refused, vote.Outcome);
            Assert.DoesNotContain(await db.ActivityRecords.Where(row => row.Reference == reference && row.Outcome == ActivityRecord.Succeeded).ToListAsync(), row => row.Attempt == "cast-vote");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Sign_in_page_keeps_the_failure_sentence_and_a_reference()
    {
        await _database.ResetAsync();
        await using var app = new ObservabilityApp(_database.ConnectionString);
        using var client = Browser(app);
        await Register(client, "ada@example.com");
        var wrong = await Post(client, "/account/sign-in", "/account/sign-in",
        [
            new("Email", "ada@example.com"),
            new("Password", "not-the-password"),
        ]);
        var wrongPage = await wrong.Content.ReadAsStringAsync();
        Assert.Contains(AccountCommands.SignInFailureMessage, wrongPage, StringComparison.Ordinal);
        Assert.Matches(ReferencePattern, ReferenceOn(wrongPage));

        using var stranger = Browser(app);
        var unknown = await Post(stranger, "/account/sign-in", "/account/sign-in",
        [
            new("Email", "nobody@example.com"),
            new("Password", Password),
        ]);
        var unknownPage = await unknown.Content.ReadAsStringAsync();
        Assert.Contains(AccountCommands.SignInFailureMessage, unknownPage, StringComparison.Ordinal);
        var reference = ReferenceOn(unknownPage);
        Assert.Matches(ReferencePattern, reference);
        Assert.DoesNotContain("nobody@example.com", reference, StringComparison.Ordinal);
        await using var db = _database.CreateContext();
        var row = await db.ActivityRecords.SingleAsync(item => item.Reference == reference);
        Assert.Equal(ActivityRecord.UnknownAccount, row.Reason);
        Assert.DoesNotContain("nobody@example.com", pageText(row));
    }

    private static string pageText(ActivityRecord row) =>
        string.Join('\n', row.Reference, row.Attempt, row.Outcome, row.Reason);

    private static string ReferenceOn(string html)
    {
        var match = Regex.Match(html, ">\\s*([23456789ABCDEFGHJKMNPQRSTVWXYZ]{8})\\s*<");
        Assert.True(match.Success, html);
        return match.Groups[1].Value;
    }

    private HttpClient Browser(ObservabilityApp app) =>
        app.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });

    private static async Task Register(HttpClient client, string email)
    {
        var response = await Post(client, "/account/register", "/account/register",
        [
            new("Email", email),
            new("Password", Password),
        ]);
        Assert.StartsWith("/groups/new", RedirectPath(response), StringComparison.Ordinal);
    }

    private static async Task CreateGroup(HttpClient client, string name, string displayName)
    {
        var response = await Post(client, "/groups/new", "/groups",
        [
            new("Name", name),
            new("DisplayName", displayName),
        ]);
        Assert.StartsWith("/catalog", RedirectPath(response), StringComparison.Ordinal);
    }

    private static async Task AddMember(HttpClient client, string displayName)
    {
        var response = await Post(client, "/members", "/members", [new("DisplayName", displayName)]);
        Assert.StartsWith("/members", RedirectPath(response), StringComparison.Ordinal);
    }

    private static async Task<HttpResponseMessage> Post(
        HttpClient client,
        string formPath,
        string action,
        IReadOnlyList<KeyValuePair<string, string>> fields)
    {
        var html = await client.GetStringAsync(formPath);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, html);
        var body = fields.Append(new KeyValuePair<string, string>("__RequestVerificationToken", match.Groups[1].Value));
        var response = await client.PostAsync(action, new FormUrlEncodedContent(body));
        if ((int)response.StatusCode >= 500)
            Assert.Fail(await response.Content.ReadAsStringAsync());
        return response;
    }

    private static string RedirectPath(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        return location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString;
    }
}
