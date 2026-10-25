using System.Globalization;
using System.Text;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;

namespace Aw.Sindri.Host.Text;

/// <summary>
/// The text form of context results (context spec §8.4 "Text output"): a header with the anchors and map version, a
/// DEGRADED line per missing source, conflicts before the sections, sections as headings with [Cn] markers and
/// '(draft)' on unconfirmed map items, then the sources table. Every state is a word; there is never colour, so
/// NO_COLOR holds. Untrusted text keeps its fence.
/// </summary>
internal static class ContextText
{
    internal static string Render(ContextResult result)
    {
        var text = new StringBuilder();
        var anchors = result.Anchors.Count == 0 ? "none" : string.Join(", ", result.Anchors);
        _ = text.AppendLine(Invariant($"context {result.QueryId.Value} · anchors: {anchors} · map: {result.MapVersion ?? "none"} · as of {result.AsOf:O}"));
        if (result.Diff is { } diff)
        {
            Diff(text, diff);
        }

        foreach (var degraded in result.Degraded)
        {
            _ = text.AppendLine(Invariant($"DEGRADED {degraded.Source.Value}: {degraded.Code} {degraded.Message}"));
        }

        foreach (var conflict in result.Conflicts)
        {
            _ = text.AppendLine(Invariant($"CONFLICT {conflict.Node}: {conflict.Summary} (intent {Markers(conflict.Intent)}; behavior {Markers(conflict.Behavior)})"));
        }

        if (result.Sections.Count == 0)
        {
            _ = text.AppendLine("no results");
        }

        foreach (var section in result.Sections)
        {
            Section(text, section);
        }

        Citations(text, result.Cites);
        return text.ToString();
    }

    /// <summary>The diff shape's header and the paths no node claims, before the sections; control characters in a path are shown escaped.</summary>
    private static void Diff(StringBuilder text, DiffPart diff)
    {
        var c = diff.Counts;
        _ = text.AppendLine(Invariant($"diff {Short(diff.Base)}..{Short(diff.Head)} against merge base {Short(diff.MergeBase)} · {c.Changed} changed: {c.Mapped} mapped, {c.Suggested} suggested, {c.Unmapped} unmapped, {c.Denied} denied"));
        if (diff.Truncated)
        {
            _ = text.AppendLine(Invariant($"  file list truncated to the first {diff.Files.Count}"));
        }

        if (diff.Unmapped.Count > 0)
        {
            _ = text.AppendLine("UNMAPPED (changed files no node claims):");
            foreach (var path in diff.Unmapped)
            {
                _ = text.Append("  ").AppendLine(Escaped(path));
            }
        }
    }

    private static string Short(string sha) => sha.Length <= 7 ? sha : sha[..7];

    private static string Escaped(string path) =>
        string.Concat(path.Select(ch => char.IsControl(ch) ? Invariant($"\\x{(int)ch:x2}") : ch.ToString()));

    internal static string Render(SourcesView view)
    {
        var text = new StringBuilder(Invariant($"sessions: {(view.SessionsEnabled ? "on" : "off")} (context.sessions.enabled)\n"));
        foreach (var row in view.Sources)
        {
            var sync = row.LastSync is { } at ? Invariant($", last sync {at:O}") : string.Empty;
            _ = text.AppendLine(Invariant($"{row.Source.Value,-10} {row.Repo?.Value ?? "-",-16} {Word(row.State),-11} {row.Detail}{sync}"));
        }

        return view.Sources.Count == 0 ? text.AppendLine("no sources: the profile maps no repo").ToString() : text.ToString();
    }

    private static void Section(StringBuilder text, ResultSection section)
    {
        _ = text.AppendLine().Append("## ").AppendLine(section.Name);
        foreach (var item in section.Items)
        {
            Item(text, item);
        }

        _ = text.AppendLine();
    }

    /// <summary>One item: its markers and title ('(draft)' when a map item isn't confirmed), trust and why, an affects item's depth and path, then its body.</summary>
    private static void Item(StringBuilder text, ResultItem item)
    {
        var draft = item.Node is not null && !item.Confirmed ? " (draft)" : string.Empty;
        _ = text.AppendLine(Invariant($"{Markers(item.Cites)} {item.Title ?? item.Node ?? item.Kind}{draft}"));
        _ = text.AppendLine(Invariant($"    trust {TrustWord(item.Trust)} · {string.Join(", ", item.Why)}"));
        if (item is { Depth: { } depth, Path: { } path })
        {
            _ = text.AppendLine(Invariant($"    depth {depth} · path {string.Join(" → ", path)}"));
        }

        foreach (var line in Body(item))
        {
            _ = text.Append("    ").AppendLine(line);
        }
    }

    private static string TrustWord(Trust trust) => trust == Trust.Trusted ? "trusted" : "untrusted";

    /// <summary>An item's body lines: its text; '(text withheld)' for a record with none; nothing for a map item with no facts line.</summary>
    private static string[] Body(ResultItem item) => (item.Text ?? (item.Node is null ? "(text withheld)" : null))?.Split('\n') ?? [];

    private static void Citations(StringBuilder text, IReadOnlyDictionary<string, Citation> cites)
    {
        if (cites.Count == 0)
        {
            return;
        }

        _ = text.AppendLine().AppendLine("sources:");
        foreach (var (marker, cite) in cites.OrderBy(c => int.Parse(c.Key.AsSpan(1), CultureInfo.InvariantCulture)))
        {
            var key = cite.Aliases.Count > 0 ? $"{cite.Key.Value} ({cite.Aliases[0]})" : cite.Key.Value;   // the first alias only; --json has them all
            foreach (var reference in cite.Refs)
            {
                var current = reference.Current is { } now ? " → " + Short(now) : string.Empty;
                _ = text.AppendLine(Invariant($"  [{marker}] {key}  {reference.Source.Value} {reference.Id.Value}  version {Short(reference.Version)}{current}  {Word(cite.State)}"));
            }
        }
    }

    private static string Markers(IReadOnlyList<string> markers) => string.Join(' ', markers.Select(m => "[" + m + "]"));

    /// <summary>A git object id (hex, longer than 12) cut to 12 characters; any other version, such as an Onyx 'updated_at' timestamp, in full.</summary>
    private static string Short(SourceVersion version) =>
        version.Value.Length > 12 && version.Value.All(char.IsAsciiHexDigitLower) ? version.Value[..12] : version.Value;

    private static string Word(CitationState state) => state switch
    {
        CitationState.Current => "current",
        CitationState.Stale => "stale",
        CitationState.Unchecked => "unchecked",
        CitationState.Unstable => "unstable",
        CitationState.ScrubBlocked => "scrub-blocked",
        _ => "unknown",
    };

    private static string Word(SourceState state) => state switch
    {
        SourceState.Ok => "ok",
        SourceState.Degraded => "degraded",
        SourceState.Unavailable => "unavailable",
        _ => "unknown",
    };

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
