using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quorum.App.Features.Shared;
using Quorum.App.Infrastructure.Persistence;

namespace Quorum.App.Features.Catalog.Browse;

public sealed class DesignPageModel : SignedInPageModel
{
    private readonly QuorumDbContext _db;

    public DesignPageModel(QuorumDbContext db)
    {
        _db = db;
    }

    public string Title { get; private set; } = "";
    public string Level { get; private set; } = "";
    public string Markdown { get; private set; } = "";

    public async Task<IActionResult> OnGetAsync(string designKey, CancellationToken cancellationToken)
    {
        var gate = await RequireGroupAsync(_db, cancellationToken);
        if (gate is not null)
            return gate;

        var design = await _db.WorkedDesigns.FirstOrDefaultAsync(row => row.Key == designKey, cancellationToken);
        if (design is null)
            return NotFound();

        Title = design.Title;
        Level = design.Level;
        Markdown = design.Markdown;
        return Page();
    }
}
