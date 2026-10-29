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
            return "no record source is set up (fix: add one to the profile's context section)\n";
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

        return text.ToString();
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
