using System.Text.Json;
using Aw.Context.Core.Map;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The pins read's 'moved' field (#1701), taken from the jobs that already hold the facts: the newest succeeded refresh that made the current map and stored moves says what each pin gained and lost (<see cref="RefreshContextMapResult.Moves"/>), and the
/// relinks after it say how many records each pin gained or lost. Only a refresh of the version that is current now counts. The jobs' results are read as JSON, like <see cref="PinRelinkDecision"/> reads the relink's.
/// </summary>
internal static class PinMovedRead
{
    /// <summary>The moved field of each of the given pins.</summary>
    /// <param name="refreshes">Every refresh job.</param>
    /// <param name="relinks">Every relink job.</param>
    /// <param name="current">The current map version, or null when there is none.</param>
    /// <param name="pins">The ids of the pins to answer for.</param>
    /// <returns>Each pin's id to its field, or null when no succeeded refresh made the current map with moves. A pin with no stored move (added or changed since, or its concept is gone) is left out.</returns>
    internal static Dictionary<string, ContextPinMoved>? Of(IReadOnlyList<JobRow> refreshes, IReadOnlyList<JobRow> relinks, int? current, IEnumerable<string> pins)
    {
        if (current is not { } version || Stored(refreshes, version) is not { } refresh)
        {
            return null;
        }

        var ended = refresh.Job.EndedAt ?? default;
        var failed = relinks.Where(j => j.EndedAt is not null && j.StartedAt > ended).MaxBy(static j => j.EndedAt) is { State: JobState.Failed };
        var counts = failed ? null : Counts(relinks, version, ended);
        var (from, to) = (MapVersionRef.Tag(Int(refresh.Moves.Values.First(), "fromVersion")), MapVersionRef.Tag(Int(refresh.Moves.Values.First(), "toVersion")));
        var moved = new Dictionary<string, ContextPinMoved>(StringComparer.Ordinal);
        foreach (var id in pins.Distinct(StringComparer.Ordinal).Where(refresh.Moves.ContainsKey))
        {
            var move = refresh.Moves[id];
            var delta = counts is not null && counts.TryGetValue(id, out var records) ? records : (int?)null;
            moved[id] = new ContextPinMoved(from, to, Int(move, "gained"), Int(move, "lost"), Places(move), delta) { RelinkFailed = failed };
        }

        return moved;
    }

    // The newest succeeded refresh that made this map version and stored at least one move (an entry is what says which two versions were compared). A newer refresh that
    // published nothing (its draft held, or "already current", which is also where a retry after a recorder error ends) has no moves and never hides this one.
    private static (JobRow Job, Dictionary<string, JsonElement> Moves)? Stored(IReadOnlyList<JobRow> refreshes, int version)
    {
        (JobRow Job, Dictionary<string, JsonElement> Moves)? newest = null;
        foreach (var job in refreshes.Where(static j => j is { State: JobState.Succeeded, EndedAt: not null, Result.ValueKind: JsonValueKind.Object }).OrderByDescending(static j => j.EndedAt))
        {
            if (job.Result is not { } result || !Is(result, "version", version) || !result.TryGetProperty("moves", out var moves) || moves.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var stored = moves.EnumerateArray().Where(static m => Text(m, "pin") is not null).ToDictionary(static m => Text(m, "pin")!, StringComparer.Ordinal);   // why: the filter keeps only moves with a pin
            if (stored.Count > 0)
            {
                newest = (job, stored);
                break;
            }
        }

        return newest;
    }

    private static List<ContextPinMovedTo> Places(JsonElement move) =>
        move.TryGetProperty("to", out var places) && places.ValueKind == JsonValueKind.Array ? [.. places.EnumerateArray().Select(Place)] : [];

    private static ContextPinMovedTo Place(JsonElement place) =>
        new(Text(place, "id"), Text(place, "name"), Int(place, "features"));

    // Every succeeded relink of this map version that started after the refresh: each pin's records after it minus before it, summed over the relinks that name the pin.
    private static Dictionary<string, int>? Counts(IReadOnlyList<JobRow> relinks, int version, DateTimeOffset since)
    {
        Dictionary<string, int>? sums = null;
        foreach (var relink in relinks.Where(j => j is { State: JobState.Succeeded, Result.ValueKind: JsonValueKind.Object } && j.StartedAt > since))
        {
            if (relink.Result is not { } result || !Is(result, "mapVersion", version) || !result.TryGetProperty("pins", out var pins) || pins.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            sums ??= new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var pin in pins.EnumerateArray().Where(static p => Text(p, "id") is not null))
            {
                var id = Text(pin, "id")!;   // why: the filter keeps only pins with an id
                sums[id] = (sums.TryGetValue(id, out var sum) ? sum : 0) + Int(pin, "after") - Int(pin, "before");
            }
        }

        return sums;
    }

    private static bool Is(JsonElement result, string name, int expected) =>
        result.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) && number == expected;

    private static int Int(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : 0;

    private static string? Text(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
