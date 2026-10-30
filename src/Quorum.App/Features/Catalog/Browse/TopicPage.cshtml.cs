using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quorum.App.Features.Shared;
using Quorum.App.Infrastructure.Persistence;

namespace Quorum.App.Features.Catalog.Browse;

public sealed class TopicPageModel : SignedInPageModel
{
    private readonly QuorumDbContext _db;

    public TopicPageModel(QuorumDbContext db)
    {
        _db = db;
    }

    public string Title { get; private set; } = "";
    public string Level { get; private set; } = "";
    public string YouCan { get; private set; } = "";
    public string Body { get; private set; } = "";
    public int PickCount { get; private set; }
    public bool Reviewed { get; private set; }
    public IReadOnlyList<SectionRow> Sections { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string topicKey, CancellationToken cancellationToken)
    {
        var gate = await RequireGroupAsync(_db, cancellationToken);
        if (gate is not null)
            return gate;

        var topic = await _db.Topics.FirstOrDefaultAsync(row => row.Key == topicKey, cancellationToken);
        if (topic is null)
            return NotFound();

        Title = topic.Title;
        Level = topic.Level;
        YouCan = topic.YouCan;
        Body = topic.Body;
        var progress = await _db.TopicProgress.FirstOrDefaultAsync(
            row => row.GroupId == OpenGroupId && row.TopicId == topic.Id,
            cancellationToken);
        PickCount = progress?.PickCount ?? 0;
        Reviewed = PickCount >= 1;

        var sections = await _db.TopicSections
            .Where(section => section.TopicId == topic.Id)
            .OrderBy(section => section.SortOrder)
            .ToListAsync(cancellationToken);
        var questions = await _db.Questions
            .Where(question => question.TopicId == topic.Id)
            .OrderBy(question => question.SortOrder)
            .ToListAsync(cancellationToken);
        Sections = sections.Select(section => new SectionRow(
            section.Key,
            SectionText.Title(section.Key),
            section.Markdown,
            section.Key switch
            {
                "review" => questions.Where(question => question.Kind == QuestionKind.Review).Select(question => question.Text).ToList(),
                "interview" => questions.Where(question => question.Kind == QuestionKind.Interview).Select(question => question.Text).ToList(),
                _ => [],
            })).ToList();
        return Page();
    }

    public sealed record SectionRow(string Key, string Title, string Markdown, IReadOnlyList<string> Questions);
}
