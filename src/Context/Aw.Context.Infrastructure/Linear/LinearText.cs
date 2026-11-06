using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>The raw text of a fetched Linear record (the engine sanitizes, scrubs and trust-tags it): whole, with no cut.</summary>
internal static partial class LinearText
{
    /// <summary>
    /// An issue: its description, its comments, its project, its relations, every attachment, and the images found in all three. The ingest
    /// sanitizer removes markdown and HTML images from the stored text, so their alt text and link are listed again as
    /// plain lines (owner q105, 2026-10-24).
    /// </summary>
    /// <param name="issue">The issue.</param>
    /// <param name="writeUserId">The write key's Linear user id: its comments that carry Sindri's marker line are left out, so Sindri's own comment never changes the record's text. Null: none are.</param>
    public static string Of(LinearIssue issue, ExternalId? writeUserId = null) => IssueHead(issue, writeUserId) + IssueTail(issue, writeUserId);

    /// <summary>A Linear Document: its content.</summary>
    public static string Of(LinearDocument document) => document.Content ?? "";

    /// <summary>A project: its description, a line each for state, lead, teams and dates, and its latest updates (newest first).</summary>
    public static string Of(LinearProject project, IReadOnlyList<LinearProjectUpdate> updates)
    {
        ArgumentNullException.ThrowIfNull(updates);
        return (project.Description ?? "") + "\n\n" + string.Join('\n', new[]
        {
            Line("State", project.State ?? "unknown"),
            Line("Lead", project.Lead?.Name),
            Line("Teams", string.Join(", ", (project.Teams?.Nodes ?? []).Select(t => t.Key))),
            Line("Start", project.StartDate),
            Line("Target", project.TargetDate),
        }.Where(l => l.Length > 0)) + Section("Updates", updates.Select(u => $"- {u.Health ?? "unknown"} ({Day(u.CreatedAt)}): {u.Body}"), more: null);
    }

    /// <summary>A project update: its health, then its body.</summary>
    public static string Of(LinearProjectUpdate update) => $"Health: {update.Health ?? "unknown"}\n\n{update.Body}";

    /// <summary>A day as 'yyyy-MM-dd' (UTC).</summary>
    public static string Day(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    // The comments the text keeps: the feed brings the newest 50 for the side table, the text keeps the first 20 of them that aren't Sindri's own.
    private static List<LinearComment> Shown(LinearIssue issue, ExternalId? writeUserId, out bool more)
    {
        var all = (issue.Comments?.Nodes ?? []).Where(c => !LinearMarker.IsSindri(c.Body, LinearExternalIds.Of(c.User), writeUserId)).ToList();
        more = all.Count > TextComments || issue.Comments?.PageInfo is { HasNextPage: true };
        return [.. all.Take(TextComments)];
    }

    private const int TextComments = 20;

    private static string IssueHead(LinearIssue issue, ExternalId? writeUserId)
    {
        var shown = Shown(issue, writeUserId, out var more);
        return issue.Description + Section("Comments", shown.Select(c => $"- {c.User?.Name ?? "unknown"} ({Day(c.CreatedAt)}): {c.Body}"), more ? "(more comments not shown)" : null);
    }

    private static string IssueTail(LinearIssue issue, ExternalId? writeUserId) =>
        (issue.Project?.Name is { Length: > 0 } project ? $"\n\nProject: {project}" : "")
        + Section("Relations", RelationLines(issue), more: null)
        + Section("Attachments", (issue.Attachments?.Nodes ?? []).Select(a => $"- {a.Title ?? Host(a.Url)} ({a.SourceType ?? "link"}) {a.Url}"), More(issue.Attachments, "attachments"))
        + Section("Images", ImageLines(issue, writeUserId), more: null);

    private static IEnumerable<string> RelationLines(LinearIssue issue) =>
        (issue.Relations?.Nodes ?? []).Select(r => $"- {r.Type} {Ref(r.RelatedIssue)}")
            .Concat((issue.InverseRelations?.Nodes ?? []).Select(r => $"- (from) {r.Type} {Ref(r.Issue)}"));

    private static IEnumerable<string> ImageLines(LinearIssue issue, ExternalId? writeUserId) =>
        ImagesIn(issue.Description).Concat(Shown(issue, writeUserId, out _).SelectMany(c => ImagesIn(c.Body)))
            .Concat((issue.Attachments?.Nodes ?? []).Where(IsImage).Select(a => (Alt: a.Title ?? "image", a.Url)))
            .DistinctBy(i => i.Url, StringComparer.Ordinal)
            .Select(i => $"- {i.Alt} {i.Url}");

    private static string Line(string label, string? value) => string.IsNullOrEmpty(value) ? "" : $"{label}: {value}";

    private static string Section(string title, IEnumerable<string> lines, string? more) =>
        lines.ToList() is { Count: > 0 } kept ? $"\n\n{title}:\n" + string.Join('\n', kept) + (more is null ? "" : "\n" + more) : "";

    // The mark a list leaves when Linear has more than the first page of it.
    private static string? More<T>(LinearConnection<T>? connection, string what) =>
        connection?.PageInfo is { HasNextPage: true } ? $"(more {what} not shown)" : null;

    private static string Ref(LinearIssueRef? issue) => issue is null ? "unknown" : $"{issue.Identifier} {issue.Title}".TrimEnd();

    private static string Host(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Host.Length > 0 ? uri.Host : url;

    private static bool IsImage(LinearAttachment attachment) =>
        Uri.TryCreate(attachment.Url, UriKind.Absolute, out var uri)
        && (string.Equals(uri.Host, "uploads.linear.app", StringComparison.OrdinalIgnoreCase)
            || _imageExtensions.Contains(Path.GetExtension(uri.AbsolutePath), StringComparer.OrdinalIgnoreCase));

    private static readonly string[] _imageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".webp"];

    private static IEnumerable<(string Alt, string Url)> ImagesIn(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        foreach (Match m in MarkdownImage.Matches(text))
        {
            yield return (Alt(m.Groups["alt"].Value), m.Groups["url"].Value);
        }

        foreach (Match tag in HtmlImage.Matches(text))
        {
            if (HtmlSrc.Match(tag.Value) is { Success: true } src)
            {
                yield return (HtmlAlt.Match(tag.Value) is { Success: true } alt ? Alt(alt.Groups["v"].Value) : "image", src.Groups["v"].Value);
            }
        }
    }

    private static string Alt(string alt) => alt.Trim() is { Length: > 0 } trimmed ? trimmed.ReplaceLineEndings(" ") : "image";

    [GeneratedRegex("!\\[(?<alt>[^\\]\\n]{0,500})\\]\\(\\s*<?(?<url>[^\\s)>]+)>?(?:\\s+\"[^\"\\n]*\")?\\s*\\)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex MarkdownImage { get; }

    [GeneratedRegex("<img\\b[^>]{0,4000}>", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HtmlImage { get; }

    [GeneratedRegex("\\bsrc\\s*=\\s*[\"'](?<v>[^\"']+)[\"']", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HtmlSrc { get; }

    [GeneratedRegex("\\balt\\s*=\\s*[\"'](?<v>[^\"']*)[\"']", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HtmlAlt { get; }
}
