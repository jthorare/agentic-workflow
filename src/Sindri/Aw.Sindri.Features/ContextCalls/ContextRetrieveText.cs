using System.Globalization;
using System.Text;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The text of a context answer as a Step reads it (context spec §8.4 "Text output"): the anchors and map version, a DEGRADED line per missing source, conflicts before the sections, sections as headings with [Cn] markers, then the
/// sources the markers cite. Every state is a word. It leaves out what changes with the clock (the query id, the as-of instant, a citation's checked state), so a re-fill gives the same text. Record text arrives already fenced by the engine.
/// </summary>
internal static class ContextRetrieveText
{
    internal static string Render(ContextResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var text = new StringBuilder();
        var anchors = result.Anchors.Count == 0 ? "none" : string.Join(", ", result.Anchors);
        _ = text.AppendLine(Invariant($"context retrieval · anchors: {anchors} · map: {result.MapVersion ?? "none"}"));
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

        _ = text.AppendLine(Invariant($"budget: {result.Budget.Used} of {result.Budget.Asked} tokens{(result.Budget.Estimate ? " (estimate)" : string.Empty)}{(result.Truncated ? ", truncated" : string.Empty)}"));
        Sources(text, result.Cites);
        return text.ToString();
    }

    private static void Section(StringBuilder text, ResultSection section)
    {
        _ = text.AppendLine().Append("## ").AppendLine(section.Name);
        foreach (var item in section.Items)
        {
            Item(text, item);
        }

        if (section.More.Count > 0)
        {
            _ = text.AppendLine(Invariant($"  +{section.More.Count} more (references only): {string.Join(", ", section.More)}"));
        }
    }

    private static void Item(StringBuilder text, ResultItem item)
    {
        _ = text.AppendLine(Invariant($"{Markers(item.Cites)} {item.Title ?? item.Node ?? item.Kind}{(item.Node is not null && !item.Confirmed ? " (draft)" : string.Empty)}"));
        _ = text.AppendLine(Invariant($"    trust {(item.Trust == Trust.Trusted ? "trusted" : "untrusted")} · {string.Join(", ", item.Why)}"));
        if (item is { Depth: { } depth, Path: { } path })
        {
            _ = text.AppendLine(Invariant($"    depth {depth} · path {string.Join(" → ", path)}"));
        }

        foreach (var line in Body(item))
        {
            _ = text.Append("    ").AppendLine(line);
        }
    }

    // An item's text; "(text withheld)" for a record with none; nothing for a map item with no facts line.
    private static string[] Body(ResultItem item) => (item.Text ?? (item.Node is null ? "(text withheld)" : null))?.Split('\n') ?? [];

    private static void Sources(StringBuilder text, IReadOnlyDictionary<string, Citation> cites)
    {
        if (cites.Count == 0)
        {
            return;
        }

        _ = text.AppendLine().AppendLine("sources:");
        foreach (var (marker, cite) in cites.OrderBy(static c => Number(c.Key)).ThenBy(static c => c.Key, StringComparer.Ordinal))
        {
            foreach (var reference in cite.Refs)
            {
                _ = text.AppendLine(Invariant($"  [{marker}] {cite.Key.Value}  {reference.Source.Value} {reference.Id.Value}  version {Short(reference.Version)}"));
            }
        }
    }

    private static int Number(string marker) => int.TryParse(marker.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : int.MaxValue;

    private static string Markers(IReadOnlyList<string> markers) => string.Join(' ', markers.Select(static m => "[" + m + "]"));

    // A git object id (hex, longer than 12) is cut to 12 characters; any other version, such as a map name or a timestamp, stays whole.
    private static string Short(SourceVersion version) => version.Value.Length > 12 && version.Value.All(char.IsAsciiHexDigitLower) ? version.Value[..12] : version.Value;

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
