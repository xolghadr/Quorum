using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quorum.App.Features.Groups;
using Quorum.App.Features.Shared;
using Quorum.App.Infrastructure.Logging;
using Quorum.App.SharedKernel;
using Quorum.Tests.Support;

namespace Quorum.App.Tests;

public sealed class NoToolLinkTests : IAsyncLifetime
{
    private const string Password = "correct-horse";

    private PostgresDatabase _database = null!;

    public async Task InitializeAsync() => _database = await PostgresDatabase.StartAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Pages_do_not_link_to_the_tools()
    {
        await _database.ResetAsync();
        await using var app = new ObservabilityApp(_database.ConnectionString);
        using var client = app.CreateClient(new() { AllowAutoRedirect = false, HandleCookies = true });
        await AssertNoTools(await client.GetStringAsync("/account/sign-in"));
        await AssertNoTools(await client.GetStringAsync("/account/register"));
        await AssertNoTools(await client.GetStringAsync("/status"));

        var registered = await Post(client, "/account/register", "/account/register",
        [
            new("Email", "ada@example.com"),
            new("Password", Password),
        ]);
        Assert.StartsWith("/groups/new", RedirectPath(registered), StringComparison.Ordinal);
        var created = await Post(client, "/groups/new", "/groups",
        [
            new("Name", "North"),
            new("DisplayName", "Ada"),
        ]);
        Assert.StartsWith("/catalog", RedirectPath(created), StringComparison.Ordinal);
        foreach (var path in new[] { "/catalog", "/groups", "/members", "/sessions", "/groups/new" })
            await AssertNoTools(await client.GetStringAsync(path));
    }

    [Fact]
    public async Task Export_batch_uses_the_allowlist_and_one_span()
    {
        await _database.ResetAsync();
        await using var lab = QuorumLab.Open(_database.ConnectionString);
        var owner = (await lab.Accounts.RegisterAsync("ada@example.com", Password)).Account!;
        var groupId = new GroupId((await lab.Groups.CreateAsync(owner.Id, "North", "Ada")).Id!.Value);
        var beaAccount = (await lab.Accounts.RegisterAsync("bea@example.com", Password)).Account!;
        var bea = MembershipRows.Account(groupId, beaAccount.Id, "Bea", lab.Clock.UtcNow);
        lab.Db.Memberships.Add(bea);
        await lab.Db.SaveChangesAsync();
        var root = RoadmapFiles.WriteFixture(Path.Combine(Path.GetTempPath(), "quorum-export-" + Guid.NewGuid().ToString("N")));
        try
        {
            Assert.Equal(0, await lab.Publish.RunAsync(root));
            var topicIds = await lab.Db.Topics.OrderBy(topic => topic.Key).Select(topic => topic.Id).Take(2).ToListAsync();
            var created = await lab.Sessions.CreateAsync(groupId, owner.Id, topicIds);
            var sessionId = new SessionId(created.Id!.Value);
            var adaMember = await lab.Db.Memberships.SingleAsync(member => member.AccountId == owner.Id);
            await lab.Election.SaveAttendanceAsync(groupId, owner.Id, sessionId, adaMember.Id, true);
            await lab.Election.SaveAttendanceAsync(groupId, beaAccount.Id, sessionId, bea.Id, true);
            await lab.Election.CastVoteAsync(groupId, owner.Id, sessionId, adaMember.Id, adaMember.Id);
            await lab.Election.CastVoteAsync(groupId, beaAccount.Id, sessionId, bea.Id, adaMember.Id);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        var export = new RecordingExport();
        var endpoint = "http://127.0.0.1:4318";
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Telemetry:OtlpEndpoint"] = endpoint,
        }).Build();
        await new ActivityExporter(new ProviderScopes(lab.Provider), export, config, lab.Clock).ExportOnceAsync();
        var election = Assert.Single(export.Batches, batch => batch.Rows.Any(row => row.Attempt == "elect-facilitator"));
        Assert.Equal(endpoint, election.Endpoint);
        Assert.Equal(ActivityExportBatch.Span, election.SpanName);
        Assert.Equal("quorum", election.ResourceAttributes["service.name"]);
        Assert.Equal(2, election.Rows.Count);
        Assert.All(election.Rows, row => Assert.Equal(election.Reference, row.Attributes["quorum.reference"]));
        foreach (var batch in export.Batches)
        {
            Assert.Equal(endpoint, batch.Endpoint);
            Assert.DoesNotContain(batch.Endpoint, new[] { "loki", "prometheus", "tempo", "grafana" }, new HostComparer(batch.Endpoint));
            var keys = batch.ResourceAttributes.Keys.Concat(batch.Rows.SelectMany(row => row.Attributes.Keys));
            Assert.All(keys, key => Assert.Contains(key, ActivityExportBatch.Allowlist));
            Assert.All(batch.Rows, row => Assert.Equal(new[] { "attempt", "outcome" }, new[] { "attempt", "outcome" }));
        }

        var handler = new CaptureHandler();
        var sending = new ServiceCollection();
        var sendConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Telemetry:CollectorUser"] = "quorum",
            ["Telemetry:CollectorPassword"] = "not-the-default",
        }).Build();
        sending.AddSingleton<IConfiguration>(sendConfig);
        sending.AddHttpClient("quorum-otlp").ConfigurePrimaryHttpMessageHandler(() => handler);
        await using var provider = sending.BuildServiceProvider();
        var sender = new OtlpActivityExport(provider.GetRequiredService<IHttpClientFactory>(), sendConfig);
        Assert.False(await sender.SendAsync(Batch("http://loki:3100", election), CancellationToken.None));
        Assert.False(await sender.SendAsync(Batch("http://prometheus:9090", election), CancellationToken.None));
        Assert.False(await sender.SendAsync(Batch("http://tempo:3200", election), CancellationToken.None));
        Assert.False(await sender.SendAsync(Batch("http://grafana:3000", election), CancellationToken.None));
        Assert.Empty(handler.Uris);

        Assert.True(await sender.SendAsync(Batch(endpoint, election), CancellationToken.None));
        Assert.Contains(handler.Uris, uri => uri == new Uri(endpoint + "/v1/traces"));
        Assert.Contains(handler.Uris, uri => uri == new Uri(endpoint + "/v1/metrics"));
        Assert.Contains(handler.Uris, uri => uri == new Uri(endpoint + "/v1/logs"));
        var metrics = handler.BodyFor("/v1/metrics");
        Assert.Contains("quorum.attempts", metrics, StringComparison.Ordinal);
        Assert.Contains("quorum.attempt.duration", metrics, StringComparison.Ordinal);
        Assert.Contains("quorum.storage.wait", metrics, StringComparison.Ordinal);
        Assert.Contains("\"attempt\"", metrics, StringComparison.Ordinal);
        Assert.Contains("\"outcome\"", metrics, StringComparison.Ordinal);
        Assert.DoesNotContain("quorum.account_id", metrics, StringComparison.Ordinal);
        Assert.DoesNotContain("email", metrics, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Password, metrics, StringComparison.Ordinal);
        var traces = handler.BodyFor("/v1/traces");
        Assert.Contains("quorum.attempt", traces, StringComparison.Ordinal);
        Assert.Contains(election.Reference, traces, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(traces, "\"name\":\"quorum.attempt\""));
    }

    private static ActivityExportBatch Batch(string endpoint, ActivityExportBatch source) => new()
    {
        Endpoint = endpoint,
        SpanName = source.SpanName,
        Reference = source.Reference,
        ResourceAttributes = source.ResourceAttributes,
        Rows = source.Rows,
    };

    private static async Task AssertNoTools(string html)
    {
        foreach (var word in new[] { "grafana", "loki", "prometheus", "tempo", "otel-collector", ":4318", ":3000", ":3100", ":9090", ":3200" })
            Assert.DoesNotContain(word, html, StringComparison.OrdinalIgnoreCase);
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

    private sealed class HostComparer(string endpoint) : IEqualityComparer<string>
    {
        public bool Equals(string? left, string? right) =>
            string.Equals(new Uri(endpoint).Host, right, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(string value) => value.GetHashCode(StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public List<Uri> Uris { get; } = [];
        public List<string> Bodies { get; } = [];

        public string BodyFor(string path) =>
            Bodies[Uris.FindIndex(uri => uri.AbsolutePath == path)];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uris.Add(request.RequestUri!);
            Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
