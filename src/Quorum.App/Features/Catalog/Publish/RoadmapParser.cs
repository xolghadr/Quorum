using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Quorum.App.Features.Catalog.Publish;

public sealed class CatalogSourceException : Exception
{
    public CatalogSourceException(string message) : base(message)
    {
    }

    public CatalogSourceException(string message, Exception inner) : base(message, inner)
    {
    }
}

public sealed record ParsedCatalog(IReadOnlyList<ParsedTrack> Tracks, IReadOnlyList<ParsedDesign> Designs);

public sealed record ParsedTrack(string Key, string Title, int SortOrder, IReadOnlyList<ParsedTopic> Topics);

public sealed record ParsedTopic(
    string Key,
    string Title,
    int SortOrder,
    string Level,
    string YouCan,
    string Body,
    IReadOnlyList<ParsedSection> Sections,
    IReadOnlyList<ParsedQuestion> Questions);

public sealed record ParsedSection(string Key, int SortOrder, string Markdown);

public sealed record ParsedQuestion(QuestionKind Kind, int SortOrder, string Text, string Fingerprint);

public sealed record ParsedDesign(string Key, string Title, int SortOrder, string Level, string Markdown);

public sealed partial class RoadmapParser
{
    private static readonly Dictionary<string, (string Key, int Order)> SectionKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["In one minute"] = ("in-one-minute", 1),
        ["When it matters"] = ("when-it-matters", 2),
        ["What it costs"] = ("what-it-costs", 3),
        ["A failure you should recognize"] = ("failure", 4),
        ["Learn"] = ("learn", 5),
        ["Review"] = ("review", 6),
        ["In an interview"] = ("interview", 7),
        ["Exercise"] = ("exercise", 8),
    };

    public ParsedCatalog Parse(string sourceDirectory)
    {
        if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
            throw new CatalogSourceException("The source directory cannot be read.");

        var trackDirectory = Path.Combine(sourceDirectory, "tracks");
        var designDirectory = Path.Combine(sourceDirectory, "designs");
        if (!Directory.Exists(trackDirectory) || !Directory.Exists(designDirectory))
            throw new CatalogSourceException("The source directory cannot be read.");

        var tracks = new List<ParsedTrack>();
        var trackOrder = 1;
        foreach (var file in Directory.GetFiles(trackDirectory, "*.md").OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            tracks.Add(ParseTrack(file, trackOrder));
            trackOrder++;
        }

        if (tracks.Count == 0)
            throw new CatalogSourceException("The source directory cannot be read.");

        return new ParsedCatalog(tracks, ParseDesigns(sourceDirectory, designDirectory));
    }

    private static ParsedTrack ParseTrack(string file, int sortOrder)
    {
        var lines = ReadLines(file);
        var headings = Headings(lines);
        var title = headings.FirstOrDefault(heading => heading.Level == 1).Text;
        if (string.IsNullOrWhiteSpace(title))
            title = Path.GetFileNameWithoutExtension(file);

        var trackKey = Path.GetFileNameWithoutExtension(file);
        var topics = new List<ParsedTopic>();
        var usedSlugs = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < headings.Count; index++)
        {
            var heading = headings[index];
            var own = Slice(lines, heading.Line + 1, NextHeadingLine(headings, index, lines.Length));
            if (!LevelPattern().IsMatch(own))
                continue;
            if (string.IsNullOrWhiteSpace(heading.Text))
                throw new CatalogSourceException("A topic has a level and no title.");

            var slug = Slug(heading.Text);
            if (slug.Length == 0)
                throw new CatalogSourceException("A topic has a level and no title.");
            if (!usedSlugs.Add(slug))
                slug = NextSlug(slug, usedSlugs);

            topics.Add(ParseTopic(lines, headings, index, $"{trackKey}/{slug}", heading.Text, topics.Count + 1));
        }

        return new ParsedTrack(trackKey, title, sortOrder, topics);
    }

    private static ParsedTopic ParseTopic(
        string[] lines,
        List<Heading> headings,
        int index,
        string key,
        string title,
        int sortOrder)
    {
        var own = Slice(lines, headings[index].Line + 1, NextHeadingLine(headings, index, lines.Length));
        var level = LevelPattern().Match(own).Groups[1].Value.Trim();
        var youCanMatch = YouCanPattern().Match(own);
        var youCan = youCanMatch.Success ? youCanMatch.Groups[1].Value.Trim() : "";
        var bodyParts = new List<string>();
        var ownBody = StripLabels(own);
        if (!string.IsNullOrWhiteSpace(ownBody))
            bodyParts.Add(ownBody);

        var sections = new List<ParsedSection>();
        var seenSections = new HashSet<string>(StringComparer.Ordinal);
        var parentLevel = headings[index].Level;
        for (var childIndex = index + 1; childIndex < headings.Count && headings[childIndex].Level > parentLevel; childIndex++)
        {
            if (headings[childIndex].Level != parentLevel + 1)
                continue;

            var childText = Slice(lines, headings[childIndex].Line + 1, ChildEnd(headings, childIndex, lines.Length));
            if (LevelPattern().IsMatch(Slice(lines, headings[childIndex].Line + 1, NextHeadingLine(headings, childIndex, lines.Length))))
                continue;

            if (SectionKeys.TryGetValue(headings[childIndex].Text, out var section) && seenSections.Add(section.Key))
            {
                var markdown = childText.Trim();
                if (markdown.Length > 0)
                    sections.Add(new ParsedSection(section.Key, section.Order, markdown));
                continue;
            }

            var block = (headings[childIndex].Text + "\n\n" + childText).Trim();
            if (block.Length > 0)
                bodyParts.Add(block);
        }

        var questions = QuestionsFor(sections);
        return new ParsedTopic(
            key,
            title,
            sortOrder,
            level,
            youCan,
            string.Join("\n\n", bodyParts).Trim(),
            sections.OrderBy(section => section.SortOrder).ToList(),
            questions);
    }

    private static List<ParsedQuestion> QuestionsFor(IReadOnlyList<ParsedSection> sections)
    {
        var questions = new List<ParsedQuestion>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        AddQuestions(questions, seen, QuestionKind.Review, sections.FirstOrDefault(section => section.Key == "review")?.Markdown, paragraphWhenUnlisted: false);
        AddQuestions(questions, seen, QuestionKind.Interview, sections.FirstOrDefault(section => section.Key == "interview")?.Markdown, paragraphWhenUnlisted: true);
        return questions;
    }

    private static void AddQuestions(
        List<ParsedQuestion> questions,
        HashSet<string> seen,
        QuestionKind kind,
        string? markdown,
        bool paragraphWhenUnlisted)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return;

        var numbered = NumberedItems(markdown);
        IEnumerable<string> wordings = numbered.Count > 0
            ? numbered
            : paragraphWhenUnlisted ? [markdown.Trim()] : [];

        foreach (var wording in wordings)
        {
            var normalized = Normalize(wording);
            if (normalized.Length == 0)
                continue;
            var fingerprint = Fingerprint(kind, normalized);
            if (!seen.Add(kind + "\n" + normalized))
                continue;
            questions.Add(new ParsedQuestion(kind, questions.Count(question => question.Kind == kind) + 1, normalized, fingerprint));
        }
    }

    private static List<ParsedDesign> ParseDesigns(string sourceDirectory, string designDirectory)
    {
        var order = new List<string>();
        var readme = Path.Combine(sourceDirectory, "README.md");
        if (File.Exists(readme))
        {
            foreach (Match match in DesignLinkPattern().Matches(File.ReadAllText(readme)))
            {
                var name = match.Groups[1].Value;
                if (!order.Contains(name, StringComparer.Ordinal))
                    order.Add(name);
            }
        }

        var files = Directory.GetFiles(designDirectory, "*.md")
            .OrderBy(file =>
            {
                var index = order.IndexOf(Path.GetFileName(file));
                return index < 0 ? int.MaxValue : index;
            })
            .ThenBy(file => file, StringComparer.Ordinal)
            .ToList();

        var designs = new List<ParsedDesign>();
        var sort = 1;
        foreach (var file in files)
        {
            var lines = ReadLines(file);
            var headings = Headings(lines);
            var titleHeading = headings.FirstOrDefault(heading => heading.Level == 1);
            if (string.IsNullOrWhiteSpace(titleHeading.Text))
                throw new CatalogSourceException("A topic has a level and no title.");

            var level = "";
            var levelLine = -1;
            for (var i = titleHeading.Line + 1; i < lines.Length; i++)
            {
                var match = LevelPattern().Match(lines[i]);
                if (!match.Success)
                    continue;
                level = match.Groups[1].Value.Trim();
                levelLine = i;
                break;
            }

            var start = levelLine >= 0 ? levelLine + 1 : titleHeading.Line + 1;
            designs.Add(new ParsedDesign(
                Path.GetFileNameWithoutExtension(file),
                titleHeading.Text,
                sort,
                level,
                string.Join('\n', lines[start..]).Trim()));
            sort++;
        }

        return designs;
    }

    private static List<string> NumberedItems(string markdown)
    {
        var items = new List<string>();
        string? current = null;
        var sawNumber = false;
        foreach (var raw in markdown.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            var match = NumberedItemPattern().Match(line);
            if (match.Success)
            {
                sawNumber = true;
                if (current is not null)
                    items.Add(current.Trim());
                current = match.Groups[1].Value.Trim();
                continue;
            }

            if (!sawNumber || string.IsNullOrWhiteSpace(line))
                continue;
            var trimmed = line.Trim();
            current = string.IsNullOrEmpty(current) ? trimmed : current + " " + trimmed;
        }

        if (current is not null)
            items.Add(current.Trim());
        return items;
    }

    private static string StripLabels(string text)
    {
        var kept = new List<string>();
        foreach (var line in text.Split('\n'))
        {
            if (LevelPattern().IsMatch(line) || YouCanPattern().IsMatch(line))
                continue;
            kept.Add(line.TrimEnd('\r'));
        }

        return string.Join('\n', kept).Trim();
    }

    private static string Normalize(string text) => WhitespacePattern().Replace(text.Trim(), " ");

    private static string Fingerprint(QuestionKind kind, string normalized)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(kind + "\n" + normalized));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string Slug(string title)
    {
        var builder = new StringBuilder();
        var dash = false;
        foreach (var character in title.Trim().ToLowerInvariant())
        {
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(character);
                dash = false;
            }
            else if (!dash && builder.Length > 0)
            {
                builder.Append('-');
                dash = true;
            }
        }

        return builder.ToString().Trim('-');
    }

    private static string NextSlug(string slug, HashSet<string> used)
    {
        var suffix = 2;
        string candidate;
        do
        {
            candidate = $"{slug}-{suffix}";
            suffix++;
        }
        while (!used.Add(candidate));
        return candidate;
    }

    private static string[] ReadLines(string file)
    {
        try
        {
            return File.ReadAllText(file).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }
        catch (IOException exception)
        {
            throw new CatalogSourceException("The source directory cannot be read.", exception);
        }
    }

    private static List<Heading> Headings(string[] lines)
    {
        var headings = new List<Heading>();
        var fence = false;
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                fence = !fence;
                continue;
            }

            if (fence)
                continue;
            var match = HeadingPattern().Match(line);
            if (match.Success)
                headings.Add(new Heading(match.Groups[1].Value.Length, match.Groups[2].Value.Trim(), index));
        }

        return headings;
    }

    private static int NextHeadingLine(List<Heading> headings, int index, int length) =>
        index + 1 < headings.Count ? headings[index + 1].Line : length;

    private static int ChildEnd(List<Heading> headings, int index, int length)
    {
        var level = headings[index].Level;
        for (var cursor = index + 1; cursor < headings.Count; cursor++)
        {
            if (headings[cursor].Level <= level)
                return headings[cursor].Line;
        }

        return length;
    }

    private static string Slice(string[] lines, int start, int end)
    {
        if (start >= end)
            return "";
        return string.Join('\n', lines[start..end]);
    }

    private readonly record struct Heading(int Level, string Text, int Line);

    [GeneratedRegex(@"^\s*\*\*Level:\*\*\s*(.*?)\s*$", RegexOptions.Multiline)]
    private static partial Regex LevelPattern();

    [GeneratedRegex(@"^\s*\*\*You can:\*\*\s*(.*?)\s*$", RegexOptions.Multiline)]
    private static partial Regex YouCanPattern();

    [GeneratedRegex(@"^(#{1,6})\s+(.*)$")]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"^\s*\d+[.)]\s*(.*)$")]
    private static partial Regex NumberedItemPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();

    [GeneratedRegex(@"designs/([A-Za-z0-9._-]+\.md)")]
    private static partial Regex DesignLinkPattern();
}
