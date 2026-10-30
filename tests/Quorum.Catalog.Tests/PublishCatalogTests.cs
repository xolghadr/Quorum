using Microsoft.EntityFrameworkCore;
using Quorum.App.Features.Catalog;
using Quorum.App.Features.Catalog.Publish;
using Quorum.Tests.Support;

namespace Quorum.Catalog.Tests;

public class RoadmapParserTests
{
    private readonly RoadmapParser _parser = new();

    [Fact]
    public void Parser_keeps_level_headings_and_drops_empty_duplicate_questions()
    {
        var root = RoadmapFiles.WriteFixture(Path.Combine(Path.GetTempPath(), "quorum-fixture-" + Guid.NewGuid().ToString("N")));
        try
        {
            var catalog = _parser.Parse(root);
            var track = Assert.Single(catalog.Tracks);
            Assert.Equal("01-sample", track.Key);
            Assert.Equal(3, track.Topics.Count);
            Assert.DoesNotContain(track.Topics, topic => topic.Title == "Good to have");

            var real = track.Topics[0];
            Assert.Equal("01-sample/real-topic", real.Key);
            Assert.Equal("Must-have", real.Level);
            Assert.Equal("explain the fixture.", real.YouCan);
            Assert.Equal(["in-one-minute", "review", "interview"], real.Sections.Select(section => section.Key).ToArray());
            Assert.DoesNotContain(real.Sections, section => section.Key == "when-it-matters");
            var review = real.Questions.Where(question => question.Kind == QuestionKind.Review).Select(question => question.Text).ToArray();
            Assert.Equal(["First question?", "Second question?"], review);
            var interview = real.Questions.Where(question => question.Kind == QuestionKind.Interview).Select(question => question.Text).ToArray();
            Assert.Equal(["How does it fail?"], interview);

            var nested = track.Topics[1];
            Assert.Equal("Good-to-have", nested.Level);
            Assert.Equal("01-sample/nested-topic", nested.Key);
            Assert.Contains("Body prose of the nested topic.", nested.Body);
            Assert.Empty(nested.Sections);

            var paragraph = track.Topics[2];
            var prompt = Assert.Single(paragraph.Questions);
            Assert.Equal(QuestionKind.Interview, prompt.Kind);
            Assert.Equal("This is one prompt about queues.", prompt.Text);

            var design = Assert.Single(catalog.Designs);
            Assert.Equal("sample-design", design.Key);
            Assert.Equal("Design: Sample", design.Title);
            Assert.Contains("The body of the design.", design.Markdown);
            Assert.DoesNotContain(track.Topics, topic => topic.Key.Contains("sample-design", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Senior_backend_roadmap_has_seven_tracks_seventy_two_topics_and_six_designs()
    {
        var roadmap = RoadmapFiles.FindSeniorBackend();
        Assert.NotNull(roadmap);
        var catalog = _parser.Parse(roadmap);
        Assert.Equal(7, catalog.Tracks.Count);
        Assert.Equal(72, catalog.Tracks.Sum(track => track.Topics.Count));
        Assert.Equal(6, catalog.Designs.Count);
        Assert.Contains(catalog.Tracks.SelectMany(track => track.Topics), topic => topic.Key == "04-data-and-apis/transactions-and-isolation");
        Assert.Contains(catalog.Tracks.SelectMany(track => track.Topics), topic => topic.Key == "02-code-design/hollywood-principle");
        Assert.DoesNotContain(catalog.Tracks.SelectMany(track => track.Topics), topic => topic.Title == "Good to have");
        Assert.Equal(
            ["url-shortener", "activity-feed", "chat", "rate-limiter", "file-upload", "order-checkout"],
            catalog.Designs.OrderBy(design => design.SortOrder).Select(design => design.Key).ToArray());
        Assert.DoesNotContain(catalog.Tracks.SelectMany(track => track.Topics), topic => topic.Key.Contains("url-shortener", StringComparison.Ordinal));
    }
}

public class PublishCatalogTests : IAsyncLifetime
{
    private PostgresDatabase _database = null!;

    public async Task InitializeAsync() => _database = await PostgresDatabase.StartAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Failed_publish_rolls_back_and_second_publish_writes_nothing()
    {
        await using var lab = QuorumLab.Open(_database.ConnectionString);
        var missing = await lab.Publish.RunAsync(Path.Combine(Path.GetTempPath(), "quorum-missing-" + Guid.NewGuid().ToString("N")));
        Assert.Equal(1, missing);
        Assert.Equal(0, await lab.Db.CatalogPublications.CountAsync());
        Assert.Equal(0, await lab.Db.Tracks.CountAsync());

        var bad = RoadmapFiles.WriteBadTopic(Path.Combine(Path.GetTempPath(), "quorum-bad-" + Guid.NewGuid().ToString("N")));
        var good = RoadmapFiles.WriteFixture(Path.Combine(Path.GetTempPath(), "quorum-good-" + Guid.NewGuid().ToString("N")));
        var other = RoadmapFiles.WriteSessionCatalog(Path.Combine(Path.GetTempPath(), "quorum-other-" + Guid.NewGuid().ToString("N")));
        try
        {
            Assert.Equal(1, await lab.Publish.RunAsync(bad));
            Assert.Equal(0, await lab.Db.CatalogPublications.CountAsync());
            Assert.Equal(0, await lab.Db.Topics.CountAsync());

            Assert.Equal(0, await lab.Publish.RunAsync(good));
            var topics = await lab.Db.Topics.CountAsync();
            var publication = await lab.Db.CatalogPublications.SingleAsync();
            Assert.Equal(CatalogPublication.SeniorBackendSourceKey, publication.SourceKey);
            Assert.Equal(new DateOnly(2026, 4, 1), publication.CompletedOn);
            var propertyNames = lab.Db.Model.FindEntityType(typeof(CatalogPublication))!
                .GetProperties()
                .Select(property => property.Name)
                .OrderBy(name => name)
                .ToArray();
            Assert.Equal(["CompletedOn", "Id", "SourceKey"], propertyNames);
            Assert.True(topics > 0);

            Assert.Equal(2, await lab.Publish.RunAsync(other));
            Assert.Equal(topics, await lab.Db.Topics.CountAsync());
            Assert.Equal(1, await lab.Db.CatalogPublications.CountAsync());
        }
        finally
        {
            Directory.Delete(bad, recursive: true);
            Directory.Delete(good, recursive: true);
            Directory.Delete(other, recursive: true);
        }
    }
}
