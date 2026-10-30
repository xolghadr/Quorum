using Microsoft.EntityFrameworkCore;
using Quorum.App.Infrastructure.Logging;
using Quorum.App.Infrastructure.Persistence;
using Quorum.App.SharedKernel;

namespace Quorum.App.Features.Catalog.Publish;

public sealed class PublishCatalog
{
    private readonly IServiceScopeFactory _scopes;
    private readonly RoadmapParser _parser;
    private readonly IClock _clock;
    private readonly SafeLog _log;

    public PublishCatalog(IServiceScopeFactory scopes, RoadmapParser parser, IClock clock, SafeLog log)
    {
        _scopes = scopes;
        _parser = parser;
        _clock = clock;
        _log = log;
    }

    public static int ExecuteFromCommandLine(IServiceProvider services)
    {
        var args = Environment.GetCommandLineArgs();
        var sourceFlag = Array.IndexOf(args, "--source");
        if (sourceFlag < 0 || sourceFlag + 1 >= args.Length)
        {
            using var missing = services.CreateScope();
            var publisher = missing.ServiceProvider.GetRequiredService<PublishCatalog>();
            publisher.ReportAsync("Catalog publication failed.").GetAwaiter().GetResult();
            return 1;
        }

        using var scope = services.CreateScope();
        var publish = scope.ServiceProvider.GetRequiredService<PublishCatalog>();
        return publish.RunAsync(args[sourceFlag + 1]).GetAwaiter().GetResult();
    }

    public async Task<int> RunAsync(string sourceDirectory, CancellationToken cancellationToken = default)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<QuorumDbContext>();
        _log.Begin();
        if (await db.CatalogPublications.AnyAsync(cancellationToken))
        {
            await ReportAsync("A publication already exists.");
            _log.Command("publish-catalog");
            return 2;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var parsed = _parser.Parse(sourceDirectory);
            var publicationId = Guid.CreateVersion7();
            foreach (var track in parsed.Tracks)
            {
                var trackId = Guid.CreateVersion7();
                db.Tracks.Add(new Track
                {
                    Id = trackId,
                    PublicationId = publicationId,
                    Key = track.Key,
                    Title = track.Title,
                    SortOrder = track.SortOrder,
                });
                foreach (var topic in track.Topics)
                {
                    var topicId = Guid.CreateVersion7();
                    db.Topics.Add(new Topic
                    {
                        Id = topicId,
                        TrackId = trackId,
                        Key = topic.Key,
                        Title = topic.Title,
                        SortOrder = topic.SortOrder,
                        Level = topic.Level,
                        YouCan = topic.YouCan,
                        Body = topic.Body,
                    });
                    foreach (var section in topic.Sections)
                    {
                        db.TopicSections.Add(new TopicSection
                        {
                            Id = Guid.CreateVersion7(),
                            TopicId = topicId,
                            Key = section.Key,
                            SortOrder = section.SortOrder,
                            Markdown = section.Markdown,
                        });
                    }

                    foreach (var question in topic.Questions)
                    {
                        db.Questions.Add(new Question
                        {
                            Id = Guid.CreateVersion7(),
                            TopicId = topicId,
                            Kind = question.Kind,
                            SortOrder = question.SortOrder,
                            Text = question.Text,
                            Fingerprint = question.Fingerprint,
                        });
                    }
                }
            }

            foreach (var design in parsed.Designs)
            {
                db.WorkedDesigns.Add(new WorkedDesign
                {
                    Id = Guid.CreateVersion7(),
                    PublicationId = publicationId,
                    Key = design.Key,
                    Title = design.Title,
                    SortOrder = design.SortOrder,
                    Level = design.Level,
                    Markdown = design.Markdown,
                });
            }

            await db.SaveChangesAsync(cancellationToken);
            if (await db.CatalogPublications.AnyAsync(cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                await ReportAsync("A publication already exists.");
                return 2;
            }

            db.CatalogPublications.Add(new CatalogPublication
            {
                Id = publicationId,
                SourceKey = CatalogPublication.SeniorBackendSourceKey,
                CompletedOn = _clock.Today,
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            var tracks = parsed.Tracks.Count;
            var topics = parsed.Tracks.Sum(track => track.Topics.Count);
            var designs = parsed.Designs.Count;
            await _log.RecordAsync(new ActivityWrite("publish-catalog", true, Tracks: tracks, Topics: topics, Designs: designs), cancellationToken);
            _log.Finish();
            _log.Publication(sourceDirectory, tracks, topics, designs, publicationId);
            return 0;
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            await ReportAsync("Catalog publication failed.");
            return 1;
        }
    }

    private async Task ReportAsync(string sentence)
    {
        try
        {
            _log.Begin();
            await _log.RecordAsync(new ActivityWrite("publish-catalog", false));
            _log.Finish();
            var reference = _log.CurrentReference;
            Console.Error.WriteLine(string.IsNullOrEmpty(reference) ? sentence : sentence + " " + reference);
        }
        catch
        {
            Console.Error.WriteLine(sentence);
        }
    }
}
