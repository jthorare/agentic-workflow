using System.Globalization;
using System.Text.Json;
using Aw.Sindri.Core.Home;

namespace Aw.Sindri.Infrastructure.Home;

/// <summary>
/// Reads aw-meta's '--json' answers into a <see cref="MetaTeamRead"/> (home spec §3.3, fields checked on 2026-11-12): camelCase keys, enum values as PascalCase strings.
/// A missing optional key is null; a missing list is empty; a value of the wrong type throws, and the reader makes the read unavailable.
/// </summary>
internal static class AwMetaJson
{
    /// <summary>The read from the four answers.</summary>
    /// <param name="lanesContext">'lanes-context --json': {recycleAt, lanes[], attention}.</param>
    /// <param name="status">'status --json': {mergeQueue[], lanes: {entries[]}}.</param>
    /// <param name="usage">'usage --json': {subjects[], attention}.</param>
    /// <param name="teamList">'team-list --json': {teams[]}, or null when it was not read.</param>
    /// <returns>The read.</returns>
    public static MetaTeamRead Parse(string lanesContext, string status, string usage, string? teamList)
    {
        using var context = JsonDocument.Parse(lanesContext);
        using var state = JsonDocument.Parse(status);
        using var subjects = JsonDocument.Parse(usage);
        var (repo, team) = SelectedTeam(teamList);
        var sessionNames = SessionNames(state.RootElement);
        var lanes = Items(context.RootElement, "lanes").Select(l => Lane(l, sessionNames, team)).ToList();
        var queue = Items(state.RootElement, "mergeQueue").Select(Queued).ToList();
        var usages = Items(subjects.RootElement, "subjects").Select(Usage).ToList();
        return new MetaTeamRead(Number(context.RootElement, "recycleAt") ?? 0, lanes, queue, usages, repo, Unavailable: null);
    }

    // 'status' lanes.entries[]: lane -> its tmux session name. The real box prints no entries (capture of 2026-11-21), so Lane falls back to '<team>-<lane>'.
    private static Dictionary<string, string> SessionNames(JsonElement status)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in Items(status, "lanes", "entries"))
        {
            if (Text(entry, "lane") is { } lane && Text(entry, "sessionName") is { } name)
            {
                _ = names.TryAdd(lane, name);
            }
        }

        return names;
    }

    // why: aw-meta names a lane's tmux session '<team>-<lane>' (TeamConfig: session names are "team-lane"), which is what a merge-queue entry's 'from' holds
    private static MetaLane Lane(JsonElement lane, Dictionary<string, string> sessionNames, string? team)
    {
        var name = Text(lane, "lane") ?? "";
        var session = sessionNames.TryGetValue(name, out var listed) ? listed : team is null ? null : $"{team}-{name}";
        return new MetaLane(name, session, Text(lane, "session"), Number(lane, "tokens"), Number(lane, "threshold") ?? 0, Text(lane, "verdict") ?? "Unknown");
    }

    private static MetaQueuedPr Queued(JsonElement queued) =>
        new(queued.GetProperty("pr").GetInt32(), Text(queued, "from") ?? "", Text(queued, "gate") ?? "none", Text(queued, "github"));

    private static MetaUsage Usage(JsonElement subject) =>
        new(Text(subject, "subject") ?? "", Text(subject, "state") ?? "Unknown", Text(subject, "kind"), Time(subject, "resetsAt"), Text(subject, "agent"));

    // The selected team's repository directory and name; the only team when one is listed and none is marked selected.
    private static (string? Repo, string? Team) SelectedTeam(string? teamList)
    {
        if (teamList is null)
        {
            return (null, null);
        }

        using var teams = JsonDocument.Parse(teamList);
        var all = Items(teams.RootElement, "teams").ToList();
        var chosen = all.Where(static t => t.TryGetProperty("selected", out var s) && s.ValueKind == JsonValueKind.True).ToList();
        JsonElement? team = chosen.Count == 1 ? chosen[0] : all.Count == 1 ? all[0] : null;
        return team is { } t ? (Text(t, "repo"), Text(t, "name")) : (null, null);
    }

    private static IEnumerable<JsonElement> Items(JsonElement root, params string[] path)
    {
        var at = root;
        foreach (var key in path)
        {
            if (at.ValueKind != JsonValueKind.Object || !at.TryGetProperty(key, out at))
            {
                return [];
            }
        }

        return at.ValueKind == JsonValueKind.Array ? [.. at.EnumerateArray()] : [];
    }

    private static string? Text(JsonElement element, string key) =>
        element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static long? Number(JsonElement element, string key) =>
        element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt64() : null;

    private static DateTimeOffset? Time(JsonElement element, string key) =>
        Text(element, key) is { } text ? DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) : null;
}
