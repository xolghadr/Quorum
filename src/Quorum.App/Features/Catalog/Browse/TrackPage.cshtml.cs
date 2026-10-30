using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quorum.App.Features.Sessions;
using Quorum.App.Features.Shared;
using Quorum.App.Infrastructure.Persistence;

namespace Quorum.App.Features.Catalog.Browse;

public sealed class TrackPageModel : SignedInPageModel
{
    private readonly QuorumDbContext _db;

    public TrackPageModel(QuorumDbContext db)
    {
        _db = db;
    }

    public string Title { get; private set; } = "";
    public bool Reviewed { get; private set; }
    public IReadOnlyList<TopicRow> Topics { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string trackKey, CancellationToken cancellationToken)
    {
        var gate = await RequireGroupAsync(_db, cancellationToken);
        if (gate is not null)
            return gate;

        var track = await _db.Tracks.FirstOrDefaultAsync(row => row.Key == trackKey, cancellationToken);
        if (track is null)
            return NotFound();

        Title = track.Title;
        Reviewed = await SessionCommands.TrackIsReviewed(_db, OpenGroupId!.Value, track.Id, cancellationToken);
        Topics = await _db.Topics
            .Where(topic => topic.TrackId == track.Id)
            .OrderBy(topic => topic.SortOrder)
            .Select(topic => new TopicRow(topic.Key, topic.Title))
            .ToListAsync(cancellationToken);
        return Page();
    }

    public sealed record TopicRow(string Key, string Title);
}
