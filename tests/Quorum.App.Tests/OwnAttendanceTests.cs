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

public class OwnAttendanceTests : IAsyncLifetime
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
    public async Task Two_members_mark_and_vote_for_themselves_while_the_creator_is_away()
    {
        await _database.ResetAsync();
        var root = RoadmapFiles.WriteSessionCatalog(Path.Combine(Path.GetTempPath(), "quorum-attend-" + Guid.NewGuid().ToString("N")));
        try
        {
            await using (var lab = QuorumLab.Open(_database.ConnectionString))
                Assert.Equal(0, await lab.Publish.RunAsync(root));

            var ada = Browser();
            await Register(ada, "ada@example.com");
            await CreateGroup(ada, "North", "Ada");
            var created = await Post(ada, "/sessions", "/sessions", await TwoTopics(ada));
            var sessionPath = RedirectPath(created);
            await Post(ada, "/sessions", "/account/sign-out", []);

            var bea = Browser();
            var cam = Browser();
            await Register(bea, "bea@example.com");
            await Register(cam, "cam@example.com");
            Guid beaId;
            Guid camId;
            await using (var db = _database.CreateContext())
            {
                var north = await db.Groups.SingleAsync(group => group.Name == "North");
                var beaAccount = await db.Accounts.SingleAsync(account => account.Email == "bea@example.com");
                var camAccount = await db.Accounts.SingleAsync(account => account.Email == "cam@example.com");
                var beaMember = MembershipRows.Account(north.Id, beaAccount.Id, "Bea", DateTimeOffset.UnixEpoch);
                var camMember = MembershipRows.Account(north.Id, camAccount.Id, "Cam", DateTimeOffset.UnixEpoch);
                db.Memberships.AddRange(beaMember, camMember);
                await db.SaveChangesAsync();
                beaId = beaMember.Id.Value;
                camId = camMember.Id.Value;
            }

            await SignIn(bea, "bea@example.com");
            await SignIn(cam, "cam@example.com");

            var beaPage = await bea.GetStringAsync(sessionPath);
            var camPage = await cam.GetStringAsync(sessionPath);
            AssertOneControl(beaPage, beaId, camId);
            AssertOneControl(camPage, camId, beaId);
            Assert.DoesNotContain("Record vote", beaPage, StringComparison.Ordinal);
            Assert.DoesNotContain("Record vote", camPage, StringComparison.Ordinal);

            var stolen = await Post(cam, sessionPath, $"/sessions/{SessionGuid(sessionPath)}/attendance",
            [
                new("memberId", beaId.ToString()),
                new("present", "true"),
            ]);
            Assert.Contains("error=1", RedirectPath(stolen), StringComparison.Ordinal);
            await using (var db = _database.CreateContext())
                Assert.Equal(0, await db.SessionAttendance.CountAsync());

            Assert.StartsWith(sessionPath, RedirectPath(await Post(bea, sessionPath, $"/sessions/{SessionGuid(sessionPath)}/attendance",
            [
                new("memberId", beaId.ToString()),
                new("present", "true"),
            ])), StringComparison.Ordinal);
            Assert.StartsWith(sessionPath, RedirectPath(await Post(cam, sessionPath, $"/sessions/{SessionGuid(sessionPath)}/attendance",
            [
                new("memberId", camId.ToString()),
                new("present", "true"),
            ])), StringComparison.Ordinal);

            var beaVoting = await bea.GetStringAsync(sessionPath);
            var camVoting = await cam.GetStringAsync(sessionPath);
            Assert.Equal(1, Count(beaVoting, "Record vote"));
            Assert.Equal(1, Count(camVoting, "Record vote"));
            Assert.Contains($"name=\"voterId\" value=\"{beaId}\"", beaVoting, StringComparison.Ordinal);
            Assert.DoesNotContain($"name=\"voterId\" value=\"{camId}\"", beaVoting, StringComparison.Ordinal);
            Assert.Contains($"name=\"voterId\" value=\"{camId}\"", camVoting, StringComparison.Ordinal);
            Assert.DoesNotContain($"name=\"voterId\" value=\"{beaId}\"", camVoting, StringComparison.Ordinal);

            var forged = await Post(cam, sessionPath, $"/sessions/{SessionGuid(sessionPath)}/votes",
            [
                new("voterId", beaId.ToString()),
                new("candidateId", camId.ToString()),
            ]);
            Assert.Contains("error=1", RedirectPath(forged), StringComparison.Ordinal);
            await using (var db = _database.CreateContext())
                Assert.Equal(0, await db.SessionVotes.CountAsync());

            var stranger = Browser();
            var signedOut = await stranger.GetAsync(sessionPath);
            Assert.Equal(HttpStatusCode.Redirect, signedOut.StatusCode);
            Assert.Contains("/account/sign-in", RedirectPath(signedOut), StringComparison.Ordinal);
            Assert.DoesNotContain("Alpha Signal Topic", await signedOut.Content.ReadAsStringAsync(), StringComparison.Ordinal);

            await Invite(cam, "dan@example.com");
            var dan = Browser();
            await Register(dan, "dan@example.com");
            var hidden = await dan.GetAsync(sessionPath);
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
            var hiddenHtml = await hidden.Content.ReadAsStringAsync();
            Assert.DoesNotContain("Alpha Signal Topic", hiddenHtml, StringComparison.Ordinal);
            Assert.DoesNotContain("Session 1", hiddenHtml, StringComparison.Ordinal);
            Assert.DoesNotContain("Bea", hiddenHtml, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void AssertOneControl(string html, Guid self, Guid other)
    {
        Assert.DoesNotContain("name=\"members\"", html, StringComparison.Ordinal);
        Assert.Contains("Mark present", html, StringComparison.Ordinal);
        Assert.Equal(1, Count(html, $"name=\"memberId\" value=\"{self}\""));
        Assert.DoesNotContain($"name=\"memberId\" value=\"{other}\"", html, StringComparison.Ordinal);
    }

    private static int Count(string html, string text) =>
        Regex.Matches(html, Regex.Escape(text)).Count;

    private static string SessionGuid(string path) => path.Split('/')[2];

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

    private static async Task Invite(HttpClient client, string email)
    {
        var response = await Post(client, "/members", "/members", [new("Email", email)]);
        Assert.StartsWith("/members", RedirectPath(response), StringComparison.Ordinal);
        Assert.DoesNotContain("error", RedirectPath(response), StringComparison.Ordinal);
    }

    private static async Task<IReadOnlyList<KeyValuePair<string, string>>> TwoTopics(HttpClient client)
    {
        var html = await client.GetStringAsync("/sessions");
        var ids = Regex.Matches(html, "value=\"([0-9a-fA-F-]{36})\"")
            .Select(match => match.Groups[1].Value)
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
