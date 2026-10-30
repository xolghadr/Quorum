using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quorum.App.Features.Accounts;
using Quorum.App.Infrastructure.Persistence;
using Quorum.App.SharedKernel;
using Quorum.Tests.Support;

namespace Quorum.App.Tests;

public class ReturnVisitTests : IAsyncLifetime
{
    private const string Password = "correct-horse";
    private const string Email = "ada@example.com";

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
    public async Task Return_visit_keeps_the_group_and_two_browsers_are_one_voter()
    {
        var stranger = Browser();
        var signedOut = await stranger.GetAsync("/catalog/topics/01-alpha/alpha-signal-topic");
        var signInPath = RedirectPath(signedOut);
        Assert.StartsWith("/account/sign-in", signInPath, StringComparison.Ordinal);
        var signInPage = await stranger.GetStringAsync(signInPath);
        AssertNoPrivateText(signInPage);

        var ada = Browser();
        await Register(ada, Email);
        await CreateGroup(ada, "North", "Ada");
        Assert.Contains("The answer key is not available yet.", await ada.GetStringAsync("/catalog"));
        var unavailable = await ada.GetStringAsync("/sessions");
        Assert.Contains("Sessions are not available yet.", unavailable);
        Assert.Contains("href=\"/catalog\"", unavailable, StringComparison.Ordinal);

        Dictionary<string, Guid> topics;
        var root = RoadmapFiles.WriteSessionCatalog(Path.Combine(Path.GetTempPath(), "quorum-app-" + Guid.NewGuid().ToString("N")));
        await using (var lab = QuorumLab.Open(_database.ConnectionString))
        {
            Assert.Equal(0, await lab.Publish.RunAsync(root));
            topics = await lab.Db.Topics.ToDictionaryAsync(topic => topic.Key, topic => topic.Id);
        }

        Directory.Delete(root, recursive: true);
        Assert.False(Directory.Exists(root));

        var catalog = await ada.GetStringAsync("/catalog");
        Assert.Contains("Last modification date: 2026-04-01", catalog);
        Assert.Contains("Alpha track", catalog);
        Assert.Contains("href=\"/sessions\"", catalog, StringComparison.Ordinal);
        var topic = await ada.GetStringAsync("/catalog/topics/01-alpha/alpha-signal-topic");
        Assert.Contains("QUORUM_FIXTURE_SENTENCE_ALPHA", topic);
        Assert.Contains("Pick count: 0", topic);
        Assert.DoesNotContain("Reviewed", topic);
        var design = await ada.GetStringAsync("/catalog/designs/sample-design");
        Assert.DoesNotContain("Topic1", design, StringComparison.Ordinal);
        Assert.DoesNotContain("Save topics", design, StringComparison.Ordinal);

        var created = await Post(ada, "/sessions", "/sessions",
        [
            new("Topic1", topics["01-alpha/alpha-signal-topic"].ToString()),
            new("Topic2", topics["01-alpha/beta-signal-topic"].ToString()),
            new("Topic3", topics["02-gamma/gamma-signal-topic"].ToString()),
        ]);
        var sessionPath = RedirectPath(created);
        Assert.StartsWith("/sessions/", sessionPath, StringComparison.Ordinal);
        var sessionId = sessionPath["/sessions/".Length..];
        var sessionPage = await ada.GetStringAsync(sessionPath);
        Assert.Contains("Date: 2026-04-01", sessionPage);
        Assert.Contains("Alpha Signal Topic", sessionPage);
        Assert.Contains("Beta Signal Topic", sessionPage);
        Assert.Contains("Gamma Signal Topic", sessionPage);
        Assert.Contains("not elected", sessionPage);
        var reviewed = await ada.GetStringAsync("/catalog/topics/01-alpha/alpha-signal-topic");
        Assert.Contains("Pick count: 1", reviewed);
        Assert.Contains("Reviewed", reviewed);

        var signedOutAda = await Post(ada, "/catalog", "/account/sign-out", []);
        Assert.StartsWith("/account/sign-in", RedirectPath(signedOutAda), StringComparison.Ordinal);
        var afterSignOut = await ada.GetAsync("/catalog");
        var signInAgain = RedirectPath(afterSignOut);
        Assert.StartsWith("/account/sign-in", signInAgain, StringComparison.Ordinal);
        AssertNoPrivateText(await ada.GetStringAsync(signInAgain));

        var wrong = Browser();
        var wrongResponse = await Post(wrong, "/account/sign-in", "/account/sign-in",
        [
            new("Email", Email),
            new("Password", "not-the-password"),
        ]);
        var unknown = Browser();
        var unknownResponse = await Post(unknown, "/account/sign-in", "/account/sign-in",
        [
            new("Email", "missing@example.com"),
            new("Password", Password),
        ]);
        var wrongHtml = await wrongResponse.Content.ReadAsStringAsync();
        var unknownHtml = await unknownResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, wrongResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unknownResponse.StatusCode);
        Assert.Contains(AccountCommands.SignInFailureMessage, wrongHtml);
        Assert.Contains(AccountCommands.SignInFailureMessage, unknownHtml);
        AssertNoPrivateText(wrongHtml);
        AssertNoPrivateText(unknownHtml);
        Assert.Equal(HttpStatusCode.Redirect, (await wrong.GetAsync("/sessions")).StatusCode);

        var duplicate = Browser();
        var duplicateResponse = await Post(duplicate, "/account/register", "/account/register",
        [
            new("Email", "Ada@Example.com"),
            new("Password", Password),
        ]);
        var duplicateHtml = await duplicateResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, duplicateResponse.StatusCode);
        Assert.Contains(AccountCommands.DuplicateEmailMessage, duplicateHtml);
        Assert.Equal(HttpStatusCode.Redirect, (await duplicate.GetAsync("/groups")).StatusCode);

        await Register(ada, Email, alreadyRegistered: true);
        var members = await ada.GetStringAsync("/members");
        Assert.Contains("Ada", members);
        var sessions = await ada.GetStringAsync("/sessions");
        Assert.Contains("Session 1", sessions);
        Assert.Contains("href=\"/catalog/topics/01-alpha/alpha-signal-topic\"", sessions, StringComparison.Ordinal);
        Assert.Contains("href=\"/catalog/topics/01-alpha/beta-signal-topic\"", sessions, StringComparison.Ordinal);
        Assert.Contains("href=\"/catalog/topics/02-gamma/gamma-signal-topic\"", sessions, StringComparison.Ordinal);
        Assert.Contains(">Alpha Signal Topic</a>", sessions, StringComparison.Ordinal);
        Assert.Contains(">Beta Signal Topic</a>", sessions, StringComparison.Ordinal);
        Assert.Contains(">Gamma Signal Topic</a>", sessions, StringComparison.Ordinal);
        Assert.Contains("Last modification date: 2026-04-01", await ada.GetStringAsync("/catalog"));
        Assert.Contains("Pick count: 1", await ada.GetStringAsync("/catalog/topics/02-gamma/gamma-signal-topic"));

        var secondBrowser = Browser();
        await SignIn(secondBrowser, Email);
        Assert.Contains("Ada", await secondBrowser.GetStringAsync("/members"));
        await using (var db = QuorumDbContext.Create(_database.ConnectionString))
        {
            var account = await db.Accounts.SingleAsync(row => row.Email == Email);
            Assert.Equal(1, await db.Memberships.CountAsync(member => member.AccountId == account.Id && member.RemovedAt == null));
        }

        Assert.StartsWith("/members", RedirectPath(await Post(ada, "/members", "/members", [new("DisplayName", "Bea")])));
        Guid adaId;
        Guid beaId;
        await using (var db = QuorumDbContext.Create(_database.ConnectionString))
        {
            var rows = await db.Memberships.Where(member => member.RemovedAt == null).ToListAsync();
            adaId = rows.Single(member => member.DisplayName == "Ada").Id.Value;
            beaId = rows.Single(member => member.DisplayName == "Bea").Id.Value;
        }

        Assert.StartsWith(sessionPath, RedirectPath(await Post(ada, sessionPath, $"/sessions/{sessionId}/attendance",
        [
            new("members", adaId.ToString()),
            new("members", beaId.ToString()),
        ])));
        Assert.StartsWith(sessionPath, RedirectPath(await Post(secondBrowser, sessionPath, $"/sessions/{sessionId}/votes",
        [
            new("voterId", adaId.ToString()),
            new("candidateId", beaId.ToString()),
        ])));
        await using (var db = QuorumDbContext.Create(_database.ConnectionString))
        {
            var voterId = new MemberId(adaId);
            Assert.Equal(1, await db.SessionVoters.CountAsync(voter => voter.MembershipId == voterId));
            Assert.Equal(2, await db.SessionVoters.CountAsync());
        }

        Assert.StartsWith("/account/sign-in", RedirectPath(await Post(ada, "/members", "/account/sign-out", [])));
        Assert.Equal(HttpStatusCode.Redirect, (await ada.GetAsync("/catalog")).StatusCode);
        Assert.Contains("Last modification date: 2026-04-01", await secondBrowser.GetStringAsync("/catalog"));

        var other = Browser();
        await Register(other, "other@example.com");
        await CreateGroup(other, "South", "Other");
        var missing = await other.GetAsync(sessionPath);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var missingHtml = await missing.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Alpha Signal Topic", missingHtml);
        Assert.DoesNotContain("Pick count", missingHtml);
    }

    private HttpClient Browser() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

    private static async Task Register(HttpClient client, string email, bool alreadyRegistered = false)
    {
        if (alreadyRegistered)
        {
            await SignIn(client, email);
            return;
        }

        var response = await Post(client, "/account/register", "/account/register",
        [
            new("Email", email),
            new("Password", Password),
        ]);
        Assert.StartsWith("/groups/new", RedirectPath(response), StringComparison.Ordinal);
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

    private static void AssertNoPrivateText(string html)
    {
        var visible = Regex.Replace(html, "name=\"__RequestVerificationToken\"[^>]*>", "", RegexOptions.IgnoreCase);
        Assert.DoesNotContain("catalog", visible, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("member", visible, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("session", visible, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("answer key", visible, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("answer-key", visible, StringComparison.OrdinalIgnoreCase);
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
