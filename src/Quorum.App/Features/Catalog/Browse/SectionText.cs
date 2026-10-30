namespace Quorum.App.Features.Catalog.Browse;

public static class SectionText
{
    public static string Title(string key) => key switch
    {
        "in-one-minute" => "In one minute",
        "when-it-matters" => "When it matters",
        "what-it-costs" => "What it costs",
        "failure" => "A failure you should recognize",
        "learn" => "Learn",
        "review" => "Review",
        "interview" => "In an interview",
        "exercise" => "Exercise",
        _ => key,
    };
}
