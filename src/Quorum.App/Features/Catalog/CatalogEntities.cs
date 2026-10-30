namespace Quorum.App.Features.Catalog;

public sealed class CatalogPublication
{
    public const string SeniorBackendSourceKey = "senior-backend-roadmap";

    public Guid Id { get; set; }
    public string SourceKey { get; set; } = "";
    public DateOnly CompletedOn { get; set; }
}

public sealed class Track
{
    public Guid Id { get; set; }
    public Guid PublicationId { get; set; }
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public int SortOrder { get; set; }
}

public sealed class Topic
{
    public Guid Id { get; set; }
    public Guid TrackId { get; set; }
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public int SortOrder { get; set; }
    public string Level { get; set; } = "";
    public string YouCan { get; set; } = "";
    public string Body { get; set; } = "";
}

public sealed class TopicSection
{
    public Guid Id { get; set; }
    public Guid TopicId { get; set; }
    public string Key { get; set; } = "";
    public int SortOrder { get; set; }
    public string Markdown { get; set; } = "";
}

public enum QuestionKind
{
    Review,
    Interview,
}

public sealed class Question
{
    public Guid Id { get; set; }
    public Guid TopicId { get; set; }
    public QuestionKind Kind { get; set; }
    public int SortOrder { get; set; }
    public string Text { get; set; } = "";
    public string Fingerprint { get; set; } = "";
}

public sealed class WorkedDesign
{
    public Guid Id { get; set; }
    public Guid PublicationId { get; set; }
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public int SortOrder { get; set; }
    public string Level { get; set; } = "";
    public string Markdown { get; set; } = "";
}
