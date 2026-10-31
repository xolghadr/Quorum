using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quorum.App.Features.Accounts;
using AppGroup = Quorum.App.Features.Groups.Group;
using Quorum.App.Features.Groups;
using Quorum.App.Infrastructure.Logging;
using Quorum.App.Infrastructure.Persistence;
using Quorum.App.SharedKernel;
using Quorum.Tests.Support;

namespace Quorum.App.Tests;

public class InvitationTests : IAsyncLifetime
{
    private const string Password = "correct-horse";
    private static readonly Regex ReferencePattern = new("^[23456789ABCDEFGHJKMNPQRSTVWXYZ]{8}$", RegexOptions.CultureInvariant);

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
    public async Task An_invited_person_accepts_on_the_home_and_opens_the_group()
    {
        await _database.ResetAsync();
        var ada = Browser();
        await Register(ada, "ada@example.com");
        await CreateGroup(ada, "North", "Ada");
        await Invite(ada, "bea@example.com");

        var bea = Browser();
        var beaLanding = await Register(bea, "Bea@Example.com");
        Assert.StartsWith("/groups", beaLanding, StringComparison.Ordinal);
        var beaHome = await bea.GetStringAsync("/groups");
        Assert.Contains("North", beaHome, StringComparison.Ordinal);
        Assert.Contains("Ada", beaHome, StringComparison.Ordinal);
        Assert.DoesNotContain("bea@example.com", beaHome, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Session 1", beaHome, StringComparison.Ordinal);
        Assert.Contains("Accept", beaHome, StringComparison.Ordinal);
        await Accept(bea, beaHome, "Bea");

        await Post(ada, "/catalog", "/account/sign-out", []);
        await Invite(bea, "Cam@Example.com");
        var beaMembers = await bea.GetStringAsync("/members");
        Assert.Contains("cam@example.com", beaMembers, StringComparison.Ordinal);
        Assert.Contains("Cancel", beaMembers, StringComparison.Ordinal);
        var beaGroups = await bea.GetStringAsync("/groups");
        Assert.DoesNotContain("cam@example.com", beaGroups, StringComparison.Ordinal);
        Assert.DoesNotContain("Cancel", beaGroups, StringComparison.Ordinal);

        await using (var db = _database.CreateContext())
        {
            Assert.False(await db.Accounts.AnyAsync(account => account.Email == "cam@example.com"));
            Assert.Equal(1, await db.Invitations.CountAsync(invitation => invitation.EmailKey == "cam@example.com" && invitation.AcceptedAt == null));
        }

        var stranger = Browser();
        await Register(stranger, "stranger@example.com");
        Assert.DoesNotContain("cam@example.com", await Read(stranger, "/groups"), StringComparison.Ordinal);
        Assert.DoesNotContain("North", await Read(stranger, "/members"), StringComparison.Ordinal);

        var cam = Browser();
        Assert.StartsWith("/groups", await Register(cam, "cam@example.com"), StringComparison.Ordinal);
        var camHome = await cam.GetStringAsync("/groups");
        Assert.Contains("North", camHome, StringComparison.Ordinal);
        Assert.Contains("Bea", camHome, StringComparison.Ordinal);
        Assert.DoesNotContain("Session 1", camHome, StringComparison.Ordinal);
        Assert.DoesNotContain("cam@example.com", camHome, StringComparison.Ordinal);
        await Accept(cam, camHome, "Cam");
        Assert.Contains("Catalog", await cam.GetStringAsync("/catalog"), StringComparison.Ordinal);

        var second = Browser();
        Assert.StartsWith("/catalog", await SignIn(second, "cam@example.com"), StringComparison.Ordinal);
        await using (var db = _database.CreateContext())
        {
            var account = await db.Accounts.SingleAsync(row => row.Email == "cam@example.com");
            var membership = await db.Memberships.SingleAsync(member => member.AccountId == account.Id && member.RemovedAt == null);
            Assert.Equal("Cam", membership.DisplayName);
            Assert.NotNull(membership.AccountId);
            Assert.Equal(1, await db.Memberships.CountAsync(member => member.AccountId == account.Id && member.RemovedAt == null));
        }
    }

    [Fact]
    public async Task Bad_names_and_a_full_group_leave_the_invitation_pending()
    {
        await _database.ResetAsync();
        var ada = Browser();
        await Register(ada, "ada@example.com");
        await CreateGroup(ada, "North", "Ada");
        await Invite(ada, "cam@example.com");
        await using (var db = _database.CreateContext())
        {
            var north = await db.Groups.SingleAsync();
            db.Memberships.Add(MembershipRows.NameOnly(north.Id, "Taken", DateTimeOffset.UnixEpoch));
            await db.SaveChangesAsync();
        }

        var cam = Browser();
        await Register(cam, "cam@example.com");
        var home = await cam.GetStringAsync("/groups");
        var acceptPath = AcceptPath(home);
        Assert.Contains(InvitationCommands.NameNotAcceptedMessage, await AcceptRaw(cam, acceptPath, "  "), StringComparison.Ordinal);
        Assert.Contains(InvitationCommands.NameNotAcceptedMessage, await AcceptRaw(cam, acceptPath, new string('n', 41)), StringComparison.Ordinal);
        Assert.Contains(InvitationCommands.NameNotAcceptedMessage, await AcceptRaw(cam, acceptPath, "taken"), StringComparison.Ordinal);
        await using (var db = _database.CreateContext())
        {
            Assert.Equal(1, await db.Invitations.CountAsync(invitation => invitation.AcceptedAt == null && invitation.DeclinedAt == null));
            Assert.Equal(0, await db.Memberships.CountAsync(member => member.AccountId != null && member.DisplayName == "taken"));
        }

        await using (var db = _database.CreateContext())
        {
            var north = await db.Groups.SingleAsync();
            for (var number = 2; number <= AppGroup.MemberMax; number++)
            {
                var account = new Account
                {
                    Id = AccountId.New(),
                    Email = $"seat{number}@example.com",
                    PasswordHash = "not-used",
                    CreatedAt = DateTimeOffset.UnixEpoch,
                };
                db.Accounts.Add(account);
                db.Memberships.Add(MembershipRows.Account(north.Id, account.Id, "Seat " + number, DateTimeOffset.UnixEpoch));
            }

            await db.SaveChangesAsync();
        }

        var full = await AcceptRaw(cam, acceptPath, "Cam");
        Assert.Contains(InvitationCommands.GroupFullMessage, full, StringComparison.Ordinal);
        Assert.DoesNotContain("cam@example.com", full, StringComparison.Ordinal);
        await using (var db = _database.CreateContext())
        {
            var invitation = await db.Invitations.SingleAsync(row => row.EmailKey == "cam@example.com");
            Assert.Null(invitation.AcceptedAt);
            Assert.Equal(AppGroup.MemberMax, await db.Memberships.CountAsync(member => member.AccountId != null && member.RemovedAt == null));
        }
    }

    [Fact]
    public async Task The_invite_form_refuses_duplicates_self_invites_and_a_full_room()
    {
        await _database.ResetAsync();
        var ada = Browser();
        await Register(ada, "ada@example.com");
        await CreateGroup(ada, "North", "Ada");
        var own = await InviteRaw(ada, "Ada@Example.com");
        Assert.Contains(InvitationCommands.NotAcceptedMessage, own, StringComparison.Ordinal);
        Assert.DoesNotContain("ada@example.com", own, StringComparison.OrdinalIgnoreCase);
        var reference = ReferenceOn(own);
        Assert.Matches(ReferencePattern, reference);

        await Invite(ada, "bea@example.com");
        var duplicate = await InviteRaw(ada, "BEA@example.com");
        Assert.Contains(InvitationCommands.NotAcceptedMessage, duplicate, StringComparison.Ordinal);
        Assert.Contains(InvitationCommands.NotAcceptedMessage, await InviteRaw(ada, "  "), StringComparison.Ordinal);
        Assert.Contains(InvitationCommands.NotAcceptedMessage, await InviteRaw(ada, "not-an-email"), StringComparison.Ordinal);

        var bea = Browser();
        await Register(bea, "bea@example.com");
        await Accept(bea, await bea.GetStringAsync("/groups"), "Bea");
        Assert.Contains(InvitationCommands.NotAcceptedMessage, await InviteRaw(ada, "bea@example.com"), StringComparison.Ordinal);

        for (var number = 3; number <= AppGroup.MemberMax; number++)
            await Invite(ada, $"pending{number}@example.com");
        Assert.Contains(InvitationCommands.NotAcceptedMessage, await InviteRaw(ada, "ninth@example.com"), StringComparison.Ordinal);
        await using var db = _database.CreateContext();
        Assert.Equal(AppGroup.MemberMax - 2, await db.Invitations.CountAsync(invitation => invitation.AcceptedAt == null && invitation.DeclinedAt == null && invitation.CancelledAt == null));
        Assert.False(await db.Invitations.AnyAsync(invitation => invitation.EmailKey == "ninth@example.com"));
        var refused = await db.ActivityRecords.SingleAsync(row => row.Reference == reference && row.Attempt == "invite-member");
        Assert.Equal(ActivityRecord.Refused, refused.Outcome);
        var stored = JsonSerializer.Serialize(await db.ActivityRecords.Where(row => row.Attempt == "invite-member").ToListAsync());
        Assert.DoesNotContain("ada@example.com", stored, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bea@example.com", stored, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Ada", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("Bea", stored, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Decline_and_cancel_follow_who_created_the_invitation()
    {
        await _database.ResetAsync();
        var ada = Browser();
        await Register(ada, "ada@example.com");
        await CreateGroup(ada, "North", "Ada");
        await Invite(ada, "bea@example.com");
        var bea = Browser();
        await Register(bea, "bea@example.com");
        await Accept(bea, await bea.GetStringAsync("/groups"), "Bea");
        await Invite(ada, "dan@example.com");
        var dan = Browser();
        await Register(dan, "dan@example.com");
        await Accept(dan, await dan.GetStringAsync("/groups"), "Dan");
        await Post(ada, "/members", "/account/sign-out", []);
        await Invite(bea, "cam@example.com");

        var adaAgain = Browser();
        await SignIn(adaAgain, "ada@example.com");
        await OpenOnlyGroup(adaAgain);
        var creatorMembers = await adaAgain.GetStringAsync("/members");
        Assert.Contains("cam@example.com", creatorMembers, StringComparison.Ordinal);
        Assert.Contains(">Dan<", creatorMembers, StringComparison.Ordinal);
        Assert.DoesNotContain("dan@example.com", creatorMembers, StringComparison.Ordinal);
        var danMembers = await dan.GetStringAsync("/members");
        Assert.DoesNotContain("cam@example.com", danMembers, StringComparison.Ordinal);
        Assert.DoesNotContain("cam@example.com", await dan.GetStringAsync("/groups"), StringComparison.Ordinal);

        var cam = Browser();
        await Register(cam, "cam@example.com");
        var decline = await Post(cam, "/groups", DeclinePath(await cam.GetStringAsync("/groups")), []);
        Assert.StartsWith("/groups", RedirectPath(decline), StringComparison.Ordinal);
        Assert.DoesNotContain("North", await Read(cam, "/groups"), StringComparison.Ordinal);
        await Invite(bea, "cam@example.com");
        Assert.Contains("North", await cam.GetStringAsync("/groups"), StringComparison.Ordinal);

        var blocked = await Post(dan, "/members", CancelPath(await bea.GetStringAsync("/members")), []);
        Assert.Contains("error=invite", RedirectPath(blocked), StringComparison.Ordinal);
        var blockedPage = await dan.GetStringAsync(RedirectPath(blocked));
        Assert.Contains(InvitationCommands.NotAcceptedMessage, blockedPage, StringComparison.Ordinal);
        Assert.DoesNotContain("cam@example.com", blockedPage, StringComparison.Ordinal);
        Assert.Matches(ReferencePattern, ReferenceOn(blockedPage));
        await using (var db = _database.CreateContext())
        {
            Assert.Equal(1, await db.Invitations.CountAsync(invitation => invitation.EmailKey == "cam@example.com" && invitation.CancelledAt == null && invitation.DeclinedAt == null && invitation.AcceptedAt == null));
        }

        var cancelled = await Post(bea, "/members", CancelPath(await bea.GetStringAsync("/members")), []);
        Assert.StartsWith("/members", RedirectPath(cancelled), StringComparison.Ordinal);
        Assert.DoesNotContain("cam@example.com", await bea.GetStringAsync("/members"), StringComparison.Ordinal);
        await Invite(bea, "cam@example.com");
        await Post(adaAgain, "/members", CancelPath(await adaAgain.GetStringAsync("/members")), []);
        Assert.DoesNotContain("cam@example.com", await adaAgain.GetStringAsync("/members"), StringComparison.Ordinal);
        Assert.DoesNotContain("North", await Read(cam, "/groups"), StringComparison.Ordinal);

        await using var store = _database.CreateContext();
        var rows = await store.ActivityRecords.Where(row =>
            row.Attempt == "invite-member"
            || row.Attempt == "accept-invitation"
            || row.Attempt == "decline-invitation"
            || row.Attempt == "cancel-invitation").ToListAsync();
        Assert.Contains(rows, row => row.Attempt == "decline-invitation" && row.Outcome == ActivityRecord.Succeeded);
        Assert.Contains(rows, row => row.Attempt == "cancel-invitation" && row.Outcome == ActivityRecord.Succeeded);
        Assert.Contains(rows, row => row.Attempt == "cancel-invitation" && row.Outcome == ActivityRecord.Refused);
        var stored = JsonSerializer.Serialize(rows);
        Assert.DoesNotContain("@", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("Ada", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("Bea", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("Dan", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("Cam", stored, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_pending_invitation_hides_the_group_and_one_open_group_hides_the_other()
    {
        await _database.ResetAsync();
        var root = RoadmapFiles.WriteSessionCatalog(Path.Combine(Path.GetTempPath(), "quorum-invite-" + Guid.NewGuid().ToString("N")));
        try
        {
            await using (var lab = QuorumLab.Open(_database.ConnectionString))
                Assert.Equal(0, await lab.Publish.RunAsync(root));

            var ada = Browser();
            await Register(ada, "ada@example.com");
            await CreateGroup(ada, "North", "Ada");
            var created = await Post(ada, "/sessions", "/sessions", await TwoTopics(ada));
            var sessionPath = RedirectPath(created);
            await Invite(ada, "cam@example.com");

            var cam = Browser();
            await Register(cam, "cam@example.com");
            var members = await cam.GetAsync("/members");
            var sessions = await cam.GetAsync("/sessions");
            var topic = await cam.GetAsync("/catalog/topics/01-alpha/alpha-signal-topic");
            var session = await cam.GetAsync(sessionPath);
            Assert.Equal(HttpStatusCode.Redirect, members.StatusCode);
            Assert.Equal(HttpStatusCode.Redirect, sessions.StatusCode);
            Assert.Equal(HttpStatusCode.Redirect, topic.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, session.StatusCode);
            foreach (var response in new[] { members, sessions, topic, session })
            {
                var html = await response.Content.ReadAsStringAsync();
                Assert.DoesNotContain("Alpha Signal Topic", html, StringComparison.Ordinal);
                Assert.DoesNotContain("Pick count", html, StringComparison.Ordinal);
                Assert.DoesNotContain("Ada", html, StringComparison.Ordinal);
                Assert.DoesNotContain("Session 1", html, StringComparison.Ordinal);
            }

            await CreateGroup(ada, "South", "Ada South");
            await Invite(ada, "sol@example.com");
            var sol = Browser();
            await Register(sol, "sol@example.com");
            await Accept(sol, await sol.GetStringAsync("/groups"), "Sol");
            await Invite(ada, "south-guest@example.com");
            string southSession;
            await using (var db = _database.CreateContext())
            {
                var gamma = await db.Topics.SingleAsync(topic => topic.Key == "02-gamma/gamma-signal-topic");
                var alpha = await db.Topics.SingleAsync(topic => topic.Key == "01-alpha/alpha-signal-topic");
                southSession = RedirectPath(await Post(ada, "/sessions", "/sessions",
                [
                    new("Topic1", alpha.Id.ToString()),
                    new("Topic2", gamma.Id.ToString()),
                ]));
            }

            Assert.Contains("Pick count: 1", await ada.GetStringAsync("/catalog/topics/02-gamma/gamma-signal-topic"), StringComparison.Ordinal);
            await OpenGroup(ada, "North");
            var northMembers = await ada.GetStringAsync("/members");
            Assert.DoesNotContain("Sol", northMembers, StringComparison.Ordinal);
            Assert.DoesNotContain("south-guest@example.com", northMembers, StringComparison.Ordinal);
            Assert.DoesNotContain(">South<", northMembers, StringComparison.Ordinal);
            var northSessions = await ada.GetStringAsync("/sessions");
            Assert.DoesNotContain(southSession, northSessions, StringComparison.Ordinal);
            Assert.Contains("Pick count: 0", await ada.GetStringAsync("/catalog/topics/02-gamma/gamma-signal-topic"), StringComparison.Ordinal);
            Assert.DoesNotContain("south-guest@example.com", await ada.GetStringAsync("/groups"), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Sign_in_without_an_invitation_keeps_today_landing()
    {
        await _database.ResetAsync();
        var ada = Browser();
        Assert.StartsWith("/groups/new", await Register(ada, "ada@example.com"), StringComparison.Ordinal);
        await Post(ada, "/groups/new", "/account/sign-out", []);
        Assert.StartsWith("/groups/new", await SignIn(ada, "ada@example.com"), StringComparison.Ordinal);
        await CreateGroup(ada, "North", "Ada");
        await Post(ada, "/catalog", "/account/sign-out", []);
        Assert.StartsWith("/catalog", await SignIn(ada, "ada@example.com"), StringComparison.Ordinal);
        await CreateGroup(ada, "South", "Ada South");
        await Post(ada, "/catalog", "/account/sign-out", []);
        Assert.StartsWith("/groups", await SignIn(ada, "ada@example.com"), StringComparison.Ordinal);
        Assert.DoesNotContain("/catalog", (await SignIn(Browser(), "ada@example.com")), StringComparison.Ordinal);
    }

    private HttpClient Browser() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

    private static async Task<string> Register(HttpClient client, string email)
    {
        var response = await Post(client, "/account/register", "/account/register",
        [
            new("Email", email),
            new("Password", Password),
        ]);
        return RedirectPath(response);
    }

    private static async Task<string> SignIn(HttpClient client, string email)
    {
        var response = await Post(client, "/account/sign-in", "/account/sign-in",
        [
            new("Email", email),
            new("Password", Password),
        ]);
        return RedirectPath(response);
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

    private static async Task<string> InviteRaw(HttpClient client, string email)
    {
        var response = await Post(client, "/members", "/members", [new("Email", email)]);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static async Task Accept(HttpClient client, string homeHtml, string displayName)
    {
        var path = AcceptPath(homeHtml);
        var response = await Post(client, path, path, [new("DisplayName", displayName)]);
        Assert.StartsWith("/catalog", RedirectPath(response), StringComparison.Ordinal);
    }

    private static async Task<string> AcceptRaw(HttpClient client, string path, string displayName)
    {
        var response = await Post(client, path, path, [new("DisplayName", displayName)]);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static async Task OpenOnlyGroup(HttpClient client)
    {
        var html = await client.GetStringAsync("/groups");
        var match = Regex.Match(html, "action=\"(/groups/[0-9a-fA-F-]+/open)\"");
        Assert.True(match.Success, html);
        var response = await Post(client, "/groups", match.Groups[1].Value, []);
        Assert.StartsWith("/catalog", RedirectPath(response), StringComparison.Ordinal);
    }

    private static async Task OpenGroup(HttpClient client, string name)
    {
        var html = await client.GetStringAsync("/groups");
        var match = Regex.Match(
            html,
            "<form method=\"post\"[^>]*action=\"(/groups/[0-9a-fA-F-]+/open)\"[\\s\\S]*?</form>");
        Match? found = null;
        foreach (Match form in Regex.Matches(html, "<form method=\"post\"[^>]*action=\"(/groups/[0-9a-fA-F-]+/open)\"[\\s\\S]*?</form>"))
        {
            if (form.Value.Contains(">" + name + "</button>", StringComparison.Ordinal))
                found = form;
        }

        Assert.NotNull(found);
        match = found;
        var response = await Post(client, "/groups", match.Groups[1].Value, []);
        Assert.StartsWith("/catalog", RedirectPath(response), StringComparison.Ordinal);
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

    private static string AcceptPath(string html)
    {
        var match = Regex.Match(html, "href=\"(/groups/invitations/[0-9a-fA-F-]+/accept)\"");
        Assert.True(match.Success, html);
        return match.Groups[1].Value;
    }

    private static string DeclinePath(string html)
    {
        var match = Regex.Match(html, "action=\"(/groups/invitations/[0-9a-fA-F-]+/decline)\"");
        Assert.True(match.Success, html);
        return match.Groups[1].Value;
    }

    private static string CancelPath(string html)
    {
        var match = Regex.Match(html, "action=\"(/members/invitations/[0-9a-fA-F-]+/cancel)\"");
        Assert.True(match.Success, html);
        return match.Groups[1].Value;
    }

    private static async Task<string> Read(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        return await response.Content.ReadAsStringAsync();
    }

    private static string ReferenceOn(string html)
    {
        var match = Regex.Match(html, @"\b([23456789ABCDEFGHJKMNPQRSTVWXYZ]{8})\b");
        Assert.True(match.Success, html);
        return match.Groups[1].Value;
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
