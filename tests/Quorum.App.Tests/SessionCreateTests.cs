using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quorum.App.Infrastructure.Persistence;
using Quorum.App.SharedKernel;
using Quorum.Tests.Support;

namespace Quorum.App.Tests;

public class SessionCreateTests : IAsyncLifetime
{
    private const string Password = "correct-horse";

    private PostgresDatabase _database = null!;
    private QuorumApplication _factory = null!;

    public async Task InitializeAsync()
    {
        _database = await PostgresDatabase.StartAsync();
        _factory = new QuorumApplication(_database.ConnectionString);
    }

    public async Task DisposeAsync()
    {
        _factory.Dispose();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task Only_the_creator_and_the_latest_facilitator_see_the_create_form()
    {
        await _database.ResetAsync();
        var root = RoadmapFiles.WriteSessionCatalog(Path.Combine(Path.GetTempPath(), "quorum-plan-" + Guid.NewGuid().ToString("N")));
        try
        {
            await using (var lab = QuorumLab.Open(_database.ConnectionString))
                Assert.Equal(0, await lab.Publish.RunAsync(root));

            var ada = Browser();
            await Register(ada, "ada@example.com");
            await CreateGroup(ada, "North", "Ada");
            var planner = await ada.GetStringAsync("/sessions");
            Assert.Contains("Create the session", planner, StringComparison.Ordinal);
            Assert.Contains("Topic1", planner, StringComparison.Ordinal);
            Assert.DoesNotContain("Create the next session", planner, StringComparison.Ordinal);

            var topics = await TwoTopics(ada);
            var created = await Post(ada, "/sessions", "/sessions", topics);
            var sessionPath = RedirectPath(created);
            var waiting = await ada.GetStringAsync("/sessions");
            Assert.DoesNotContain("Create the session", waiting, StringComparison.Ordinal);
            var before = await Counts();
            var blocked = await Post(ada, "/sessions", "/sessions", topics);
            Assert.Equal(HttpStatusCode.OK, blocked.StatusCode);
            Assert.Equal(before, await Counts());

            var detail = await ada.GetStringAsync(sessionPath);
            Assert.Contains("Save topics", detail, StringComparison.Ordinal);

            var bea = Browser();
            await Register(bea, "bea@example.com");
            Guid beaId;
            Guid adaId;
            await using (var db = _database.CreateContext())
            {
                var north = await db.Groups.SingleAsync();
                var beaAccount = await db.Accounts.SingleAsync(account => account.Email == "bea@example.com");
                var beaMember = MembershipRows.Account(north.Id, beaAccount.Id, "Bea", DateTimeOffset.UnixEpoch);
                db.Memberships.Add(beaMember);
                await db.SaveChangesAsync();
                beaId = beaMember.Id.Value;
                adaId = await db.Memberships.Where(member => member.DisplayName == "Ada").Select(member => member.Id.Value).SingleAsync();
            }

            await SignIn(bea, "bea@example.com");
            var beaSessions = await bea.GetStringAsync("/sessions");
            Assert.DoesNotContain("Create the session", beaSessions, StringComparison.Ordinal);
            Assert.DoesNotContain("Save topics", await bea.GetStringAsync(sessionPath), StringComparison.Ordinal);
            Assert.Equal(before, await Counts());
            var beaPost = await Post(bea, "/sessions", "/sessions", topics);
            Assert.Equal(HttpStatusCode.OK, beaPost.StatusCode);
            Assert.Equal(before, await Counts());

            await Present(ada, sessionPath, adaId);
            await Present(bea, sessionPath, beaId);
            await Vote(ada, sessionPath, adaId, beaId);
            await Vote(bea, sessionPath, beaId, beaId);
            Assert.DoesNotContain("Save topics", await ada.GetStringAsync(sessionPath), StringComparison.Ordinal);
            Assert.DoesNotContain("Save topics", await bea.GetStringAsync(sessionPath), StringComparison.Ordinal);
            Assert.Contains("Create the session", await ada.GetStringAsync("/sessions"), StringComparison.Ordinal);
            Assert.Contains("Create the session", await bea.GetStringAsync("/sessions"), StringComparison.Ordinal);

            var cam = Browser();
            await Register(cam, "cam@example.com");
            await using (var db = _database.CreateContext())
            {
                var north = await db.Groups.SingleAsync();
                var camAccount = await db.Accounts.SingleAsync(account => account.Email == "cam@example.com");
                db.Memberships.Add(MembershipRows.Account(north.Id, camAccount.Id, "Cam", DateTimeOffset.UnixEpoch));
                await db.SaveChangesAsync();
            }

            await SignIn(cam, "cam@example.com");
            Assert.DoesNotContain("Create the session", await cam.GetStringAsync("/sessions"), StringComparison.Ordinal);
            var camPost = await Post(cam, "/sessions", "/sessions", topics);
            Assert.Equal(HttpStatusCode.OK, camPost.StatusCode);
            Assert.Equal(before, await Counts());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private async Task<(int Sessions, int Gamma)> Counts()
    {
        await using var db = _database.CreateContext();
        var sessions = await db.Sessions.CountAsync();
        var gamma = await db.Topics.SingleAsync(topic => topic.Key == "02-gamma/gamma-signal-topic");
        var progress = await db.TopicProgress.SingleOrDefaultAsync(row => row.TopicId == gamma.Id);
        return (sessions, progress?.PickCount ?? 0);
    }

    private static async Task Present(HttpClient client, string sessionPath, Guid memberId)
    {
        var response = await Post(client, sessionPath, $"/sessions/{GuidOf(sessionPath)}/attendance",
        [
            new("memberId", memberId.ToString()),
            new("present", "true"),
        ]);
        Assert.StartsWith(sessionPath, RedirectPath(response), StringComparison.Ordinal);
    }

    private static async Task Vote(HttpClient client, string sessionPath, Guid voterId, Guid candidateId)
    {
        var response = await Post(client, sessionPath, $"/sessions/{GuidOf(sessionPath)}/votes",
        [
            new("voterId", voterId.ToString()),
            new("candidateId", candidateId.ToString()),
        ]);
        Assert.StartsWith(sessionPath, RedirectPath(response), StringComparison.Ordinal);
    }

    private static string GuidOf(string path) => path.Split('/')[2];

    private HttpClient Browser() =>
        _factory.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });

    private static async Task Register(HttpClient client, string email)
    {
        var response = await Post(client, "/account/register", "/account/register",
        [
            new("Email", email),
            new("Password", Password),
        ]);
        Assert.StartsWith("/groups", RedirectPath(response), StringComparison.Ordinal);
    }

    private static async Task SignIn(HttpClient client, string email)
    {
        var response = await Post(client, "/account/sign-in", "/account/sign-in",
        [
            new("Email", email),
            new("Password", Password),
        ]);
        Assert.StartsWith("/catalog", RedirectPath(response), StringComparison.Ordinal);
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

    private static async Task<IReadOnlyList<KeyValuePair<string, string>>> TwoTopics(HttpClient client)
    {
        var html = await client.GetStringAsync("/sessions");
        var ids = Regex.Matches(html, "value=\"([0-9a-fA-F-]{36})\"")
            .Select(match => match.Groups[1].Value)
            .Where(value => value != Guid.Empty.ToString())
            .Distinct()
            .Take(2)
            .ToArray();
        Assert.Equal(2, ids.Length);
        return [new("Topic1", ids[0]), new("Topic2", ids[1])];
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

    private sealed class QuorumApplication : WebApplicationFactory<Program>
    {
        private readonly string _connection;

        public QuorumApplication(string connection) => _connection = connection;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Quorum"] = _connection,
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IClock>();
                services.AddSingleton<IClock>(new FakeClock());
                services.RemoveAll<DbContextOptions<QuorumDbContext>>();
                services.RemoveAll<QuorumDbContext>();
                services.AddDbContext<QuorumDbContext>(options => options.UseNpgsql(_connection));
            });
        }
    }
}
