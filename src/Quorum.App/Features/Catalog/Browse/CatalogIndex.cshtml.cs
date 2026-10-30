using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quorum.App.Features.Shared;
using Quorum.App.Infrastructure.Persistence;

namespace Quorum.App.Features.Catalog.Browse;

public sealed class CatalogIndexModel : SignedInPageModel
{
    private readonly QuorumDbContext _db;

    public CatalogIndexModel(QuorumDbContext db)
    {
        _db = db;
    }

    public bool Published { get; private set; }
    public DateOnly? CompletedOn { get; private set; }
    public IReadOnlyList<LinkRow> Tracks { get; private set; } = [];
    public IReadOnlyList<LinkRow> Designs { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var gate = await RequireGroupAsync(_db, cancellationToken);
        if (gate is not null)
            return gate;

        var publication = await _db.CatalogPublications.SingleOrDefaultAsync(cancellationToken);
        if (publication is null)
            return Page();

        Published = true;
        CompletedOn = publication.CompletedOn;
        Tracks = await _db.Tracks
            .OrderBy(track => track.SortOrder)
            .Select(track => new LinkRow(track.Key, track.Title))
            .ToListAsync(cancellationToken);
        Designs = await _db.WorkedDesigns
            .OrderBy(design => design.SortOrder)
            .Select(design => new LinkRow(design.Key, design.Title))
            .ToListAsync(cancellationToken);
        return Page();
    }

    public sealed record LinkRow(string Key, string Title);
}
