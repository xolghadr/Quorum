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

public class LeaveGroupTests : IAsyncLifetime
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
    public async Task Leave_hides_the_session_and_keeps_the_past_vote()
    {
        await _database.ResetAsync();
        var root = RoadmapFiles.WriteSessionCatalog(Path.Combine(Path.GetTempPath(), "quorum-leave-page-" + Guid.NewGuid().ToString("N")));
        try
        {
            await using (var lab = QuorumLab.Open(_database.ConnectionString))
                Assert.Equal(0, await lab.Publish.RunAsync(root));

            var ada = Browser();
            await Register(ada, "ada@example.com");
            await CreateGroup(ada, "North", "Ada");
            var bea = Browser();
            await Register(bea, "bea@example.com");
            Guid beaId;
            Guid adaId;
            Guid legacyId;
            await using (var db = _database.CreateContext())
            {
                var north = await db.Groups.SingleAsync();
                var beaAccount = await db.Accounts.SingleAsync(account => account.Email == "bea@example.com");
                var beaMember = MembershipRows.Account(north.Id, beaAccount.Id, "Bea", DateTimeOffset.UnixEpoch);
                var legacy = MembershipRows.NameOnly(north.Id, "Legacy", DateTimeOffset.UnixEpoch);
                db.Memberships.AddRange(beaMember, legacy);
                await db.SaveChangesAsync();
                beaId = beaMember.Id.Value;
                legacyId = legacy.Id.Value;
                adaId = await db.Memberships.Where(member => member.DisplayName == "Ada").Select(member => member.Id.Value).SingleAsync();
            }

            var creator = await ada.GetStringAsync("/members");
            Assert.Contains(">Remove<", creator, StringComparison.Ordinal);
            Assert.DoesNotContain("action=\"/members/leave\"", creator, StringComparison.Ordinal);
            Assert.DoesNotContain("Legacy", creator, StringComparison.Ordinal);

            await SignIn(bea, "bea@example.com");
            var member = await bea.GetStringAsync("/members");
            Assert.Contains("action=\"/members/leave\"", member, StringComparison.Ordinal);
            Assert.DoesNotContain(">Remove<", member, StringComparison.Ordinal);
            Assert.DoesNotContain("Legacy", member, StringComparison.Ordinal);

            var topics = await TwoTopics(ada);
            var created = await Post(ada, "/sessions", "/sessions", topics);
            var sessionPath = RedirectPath(created);
            await Present(ada, sessionPath, adaId);
            await Present(bea, sessionPath, beaId);
            await Vote(ada, sessionPath, adaId, beaId);
            await Vote(bea, sessionPath, beaId, beaId);
            var blocked = await Post(ada, sessionPath, sessionPath + "/attendance",
            [
                new("memberId", legacyId.ToString()),
                new("present", "true"),
            ]);
            Assert.Contains("error=1", RedirectPath(blocked), StringComparison.Ordinal);
            var held = await ada.GetStringAsync(sessionPath);
            Assert.DoesNotContain("Legacy", held, StringComparison.Ordinal);
            Assert.Contains("Bea voted for Bea", held, StringComparison.Ordinal);

            var left = await Post(bea, "/members", "/members/leave", []);
            Assert.StartsWith("/groups", RedirectPath(left), StringComparison.Ordinal);
            var kept = await ada.GetStringAsync(sessionPath);
            Assert.Contains("Bea voted for Bea", kept, StringComparison.Ordinal);
            Assert.Contains("<li>Bea</li>", kept, StringComparison.Ordinal);
            var hidden = await bea.GetAsync(sessionPath);
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
            Assert.DoesNotContain("Alpha Signal Topic", await hidden.Content.ReadAsStringAsync(), StringComparison.Ordinal);

            var refused = await Post(ada, "/members", "/members/leave", []);
            var refusedPath = RedirectPath(refused);
            var reference = Regex.Match(refusedPath, "reference=([A-Z0-9]+)").Groups[1].Value;
            Assert.False(string.IsNullOrEmpty(reference));
            Assert.Contains(reference, await ada.GetStringAsync(refusedPath), StringComparison.Ordinal);
            Assert.Contains("Alpha Signal Topic", await ada.GetStringAsync(sessionPath), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

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

    private static async Task Present(HttpClient client, string sessionPath, Guid memberId)
    {
        var response = await Post(client, sessionPath, sessionPath + "/attendance",
        [
            new("memberId", memberId.ToString()),
            new("present", "true"),
        ]);
        Assert.StartsWith(sessionPath, RedirectPath(response), StringComparison.Ordinal);
    }

    private static async Task Vote(HttpClient client, string sessionPath, Guid voterId, Guid candidateId)
    {
        var response = await Post(client, sessionPath, sessionPath + "/votes",
        [
            new("voterId", voterId.ToString()),
            new("candidateId", candidateId.ToString()),
        ]);
        Assert.StartsWith(sessionPath, RedirectPath(response), StringComparison.Ordinal);
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
