using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// The recorded limits and their fix lines in one constant map (scoping spec §8.2), read by the CLI, the checks and the stop view so the
/// text cannot drift. A source matches a limit on (lens, state, reason) only: its code is shown, never matched.
/// </summary>
internal static class ScopeFixLines
{
    /// <summary>The four limits of the table, not-configured, anchor-missing (an asked anchor the view couldn't find) and edges-out-of-scope (edges the lens left out, #993), in the table's order.</summary>
    internal static IReadOnlyList<RecordedLimit> All { get; } =
    [
        new("index-not-built", "code", "unavailable", "index-not-built", "index-not-built", "Wait for the code index (S-9)"),
        new("history-unlinked", "history", "unavailable", "history-unlinked", "history-unlinked", "Wait for the ticket linker (S-5B)"),
        new("commit-not-held", "code", "unavailable", "commit-not-held", "commit-not-held", "Fetch the commit into the profile repo's checkout, then regather"),
        new("not-synced", "history", "unavailable", "not-synced", "never synced", "Sindri starts the source's sync itself; rescope once it has run"),
        new("not-configured", Lens: null, "not-configured", "not-configured", "not-configured", "Name the source in the profile, then regather"),
        new("anchor-missing", Lens: null, "missing", "anchor-missing", "anchor not in the map", "Check the anchor's key (the view's suggested anchors name close ones), then regather"),
        new("edges-out-of-scope", Lens: null, "unavailable", "edges-out-of-scope", "edges outside the lens", "Nothing to fix: the lens left out edges ending outside its found nodes and anchors"),
    ];

    /// <summary>The limit a source of <paramref name="lens"/> with this state and reason is, or null when it matches none (a degraded source never does).</summary>
    internal static RecordedLimit? Match(string lens, string? state, string? reason) =>
        All.FirstOrDefault(l => string.Equals(l.State, state, StringComparison.Ordinal)
            && string.Equals(l.Reason, reason, StringComparison.Ordinal)
            && (l.Lens is null || string.Equals(l.Lens, lens, StringComparison.Ordinal)));

    /// <summary>The limit with this name, or null.</summary>
    internal static RecordedLimit? ByName(string? name) => All.FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.Ordinal));

    /// <summary>The row text the stop shows: 'not searched: &lt;limit&gt; — &lt;fix line&gt;'. A never-synced source reads by its sync pair, in five sentences.</summary>
    /// <param name="limit">The limit.</param>
    /// <param name="source">The coverage source object that matched, for the not-synced sync pair; may be null.</param>
    internal static string Text(RecordedLimit limit, JsonNode? source)
    {
        ArgumentNullException.ThrowIfNull(limit);
        var fix = string.Equals(limit.Name, "not-synced", StringComparison.Ordinal) ? SyncSentence(source) : limit.Fix;
        return $"not searched: {limit.Label} — {fix}";
    }

    private static string SyncSentence(JsonNode? source)
    {
        var sync = (source as JsonObject)?["sync"] as JsonObject;
        return ScopeJson.Str(sync, "state") switch
        {
            "queued" => "sync queued",
            "running" => "sync running",
            "finished" => "synced since; rescope to include it",
            "failed" => Failed(sync),
            _ => "a sync starts shortly",
        };
    }

    private static string Failed(JsonObject? sync)
    {
        var code = ScopeJson.Str(sync, "code") ?? "no code";
        var what = $"the sync failed ({code})";
        if ((ScopeJson.Int(sync, "failures") ?? 0) >= 3)
        {
            return $"{what}; stopped after 3 failures: run `sindri context sources sync` once it's fixed";
        }

        return ScopeJson.Str(sync, "retryAt") is { } at ? $"{what}; retrying at {at}" : $"{what}; a retry is scheduled";
    }
}
