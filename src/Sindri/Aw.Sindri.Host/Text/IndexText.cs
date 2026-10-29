using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using Aw.Context.Core;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Text;

/// <summary>The text of 'sindri index status' (owned-index spec §5.8): one line per source, the state as a word, the last tick and the code it ended with.</summary>
internal static class IndexText
{
    internal static string Status(IndexStatusView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (view.Sources.Count == 0)
        {
            return "no record source is set up (fix: add one to the profile's context section)\n" + Concepts(view.Concepts);
        }

        var width = view.Sources.Max(static s => s.Source.Length);
        var text = new StringBuilder();
        foreach (var row in view.Sources)
        {
            var sync = row.LastSync is { } at ? "last sync " + at.ToString("u", CultureInfo.InvariantCulture) : "not synced yet";
            _ = text.Append(row.Source.PadRight(width)).Append("  ").Append(State(row)).Append(" · ").Append(sync);
            _ = row.LastCode is { } code && code == ContextErrors.SyncCapped.Code ? text.Append(" · ").Append(code) : text;   // a failed code is already in the state word
            _ = text.Append('\n');
        }

        return text.Append(Concepts(view.Concepts)).ToString();
    }

    /// <summary>The concepts line (#1597): empty from a server that sends none; a part whose value is unknown is left out.</summary>
    private static string Concepts(IndexConceptsView? c)
    {
        if (c is null)
        {
            return string.Empty;
        }

        if (c.Code is { } code)
        {
            return $"concepts: unavailable ({code})\n";
        }

        if (!c.Enabled)
        {
            return "concepts: off (context.conceptSynthesis or labeling is off)\n";
        }

        if (c.Concepts == 0)
        {
            return "concepts: none yet\n";
        }

        return "concepts: " + string.Join(", ", Parts(c)) + "\n";
    }

    private static List<string> Parts(IndexConceptsView c)
    {
        var parts = new List<string> { string.Create(CultureInfo.InvariantCulture, $"{c.Concepts:N0} ({c.MultiSource:N0} cite 2+ sources)"), string.Create(CultureInfo.InvariantCulture, $"{c.Queued:N0} queued") };
        if (c.Embedded is > 0 and var embedded)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{c.Covered:N0} of {embedded:N0} records covered ({Math.Round(c.Coverage * 100, MidpointRounding.AwayFromZero):0}%)"));
        }

        if (c.Pending is > 0 and var pending)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{pending:N0} not embedded yet"));
        }

        parts.Add(string.Create(CultureInfo.InvariantCulture, $"watermark {c.Watermark}"));
        return parts;
    }

    private static readonly FrozenDictionary<string, string> _reasons =
        ContextErrors.All.ToFrozenDictionary(static e => e.Code, static e => e.Title, StringComparer.Ordinal);

    private static string Failed(string code) =>
        _reasons.TryGetValue(code, out var reason) ? $"last sync failed: {code} ({reason})" : "last sync failed: " + code;

    private static string State(IndexSourceRow row) =>
        row.LastCode is { } code && code != ContextErrors.SyncCapped.Code ? Failed(code)
        : row.ReReading is { } stored ? string.Create(CultureInfo.InvariantCulture, $"re-reading for project links, {stored:N0} issues stored")
        : row.Complete ? "complete"
        : row.Usable ? "usable, filling older history"
        : row.Phase is null ? "not started"
        : "syncing recent window";

    internal static string Counts(IndexPurgeView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return string.Create(CultureInfo.InvariantCulture, $"{view.Records:N0} records, {view.Chunks:N0} chunks, {view.Vectors:N0} vectors");
    }
}
