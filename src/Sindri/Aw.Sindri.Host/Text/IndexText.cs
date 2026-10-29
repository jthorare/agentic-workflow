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
            AppendPull(text, row);
            _ = text.Append('\n');
        }

        return text.ToString();
    }

    // why: a server that predates the fields serves none of them, and the line stays as it was
    private static void AppendPull(StringBuilder text, IndexSourceRow row)
    {
        switch (row.Pull)
        {
            case "running":
                _ = text.Append(" · pulling now");
                break;
            case "backoff":
                _ = text.Append(CultureInfo.InvariantCulture, $" · backoff, {Failures(row.Failures)}").Append(row.NextTry is { } next ? ", next try " + Clock(next) : string.Empty);
                break;
            case "failed":
                _ = text.Append(CultureInfo.InvariantCulture, $" · failed, {Failures(row.Failures)}");
                break;
            default:
                break;
        }

        _ = row.LastCommit is { } commit ? text.Append(" · last commit ").Append(Clock(commit)) : text;
        _ = (string.Equals(row.Pull, "backoff", StringComparison.Ordinal) || string.Equals(row.Pull, "failed", StringComparison.Ordinal)) && row.LastErrorText is { Length: > 0 } why ? text.Append(" · ").Append(why) : text;
    }

    private static string Failures(int? count) => count == 1 ? "1 failure" : string.Create(CultureInfo.InvariantCulture, $"{count ?? 0} failures");

    private static string Clock(DateTimeOffset at) => at.UtcDateTime.ToString("HH:mm'Z'", CultureInfo.InvariantCulture);

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
