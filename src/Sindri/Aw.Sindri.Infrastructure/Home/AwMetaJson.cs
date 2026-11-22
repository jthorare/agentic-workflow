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
        var sessionNames = Items(state.RootElement, "lanes", "entries")
            .Select(static e => (Lane: Text(e, "lane"), Name: Text(e, "sessionName")))
            .Where(static e => e.Lane is not null && e.Name is not null)
            .GroupBy(static e => e.Lane!, StringComparer.Ordinal)
            .ToDictionary(static g => g.Key, static g => g.First().Name!, StringComparer.Ordinal);
        var lanes = Items(context.RootElement, "lanes").Select(l => Lane(l, sessionNames)).ToList();
        var queue = Items(state.RootElement, "mergeQueue").Select(static q => new MetaQueuedPr(q.GetProperty("pr").GetInt32(), Text(q, "from") ?? "", Text(q, "gate") ?? "none", Text(q, "github"))).ToList();
        var usages = Items(subjects.RootElement, "subjects").Select(static u => new MetaUsage(Text(u, "subject") ?? "", Text(u, "state") ?? "Unknown", Text(u, "kind"), Time(u, "resetsAt"), Text(u, "agent"))).ToList();
        return new MetaTeamRead(Number(context.RootElement, "recycleAt") ?? 0, lanes, queue, usages, RepoDirectory(teamList), Unavailable: null);
    }

    private static MetaLane Lane(JsonElement lane, Dictionary<string, string> sessionNames)
    {
        var name = Text(lane, "lane") ?? "";
        return new MetaLane(name, sessionNames.GetValueOrDefault(name), Text(lane, "session"), Number(lane, "tokens"), Number(lane, "threshold") ?? 0, Text(lane, "verdict") ?? "Unknown");
    }

    // The selected team's repository directory; the only team when one is listed and none is marked selected.
    private static string? RepoDirectory(string? teamList)
    {
        if (teamList is null)
        {
            return null;
        }

        using var teams = JsonDocument.Parse(teamList);
        var all = Items(teams.RootElement, "teams").ToList();
        var chosen = all.Where(static t => t.TryGetProperty("selected", out var s) && s.ValueKind == JsonValueKind.True).ToList();
        return chosen.Count == 1 ? Text(chosen[0], "repo") : all.Count == 1 ? Text(all[0], "repo") : null;
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
