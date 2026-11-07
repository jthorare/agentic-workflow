using System.Text.Json;
using Aw.Context.Core.Map;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The pins read's 'moved' field (#1701), taken from the jobs that already hold the facts: the newest succeeded refresh stored what each pin gained and lost (<see cref="RefreshContextMapResult.Moves"/>), and the
/// relinks after it say how many records each pin gained or lost. Only a refresh of the version that is current now counts. The jobs' results are read as JSON, like <see cref="PinRelinkDecision"/> reads the relink's.
/// </summary>
internal static class PinMovedRead
{
    /// <summary>The moved field of each of the given pins.</summary>
    /// <param name="refreshes">Every refresh job.</param>
    /// <param name="relinks">Every relink job.</param>
    /// <param name="current">The current map version, or null when there is none.</param>
    /// <param name="pins">The ids of the pins to answer for.</param>
    /// <returns>Each pin's id to its field, or null when the newest succeeded refresh did not make the current map or stored no moves.</returns>
    internal static Dictionary<string, ContextPinMoved>? Of(IReadOnlyList<JobRow> refreshes, IReadOnlyList<JobRow> relinks, int? current, IEnumerable<string> pins)
    {
        if (current is not { } version || Stored(refreshes, version) is not { } refresh)
        {
            return null;
        }

        var ended = refresh.Job.EndedAt ?? default;
        var failed = relinks.Where(j => j.EndedAt is not null && j.StartedAt > ended).MaxBy(static j => j.EndedAt) is { State: JobState.Failed };
        var counts = failed ? null : Counts(relinks, version);
        var (from, to) = (MapVersionRef.Tag(Int(refresh.Moves.Values.First(), "fromVersion")), MapVersionRef.Tag(Int(refresh.Moves.Values.First(), "toVersion")));
        return pins.Distinct(StringComparer.Ordinal).ToDictionary(static id => id, id =>
        {
            var delta = counts is not null && counts.TryGetValue(id, out var records) ? records : (int?)null;
            return refresh.Moves.TryGetValue(id, out var move)
                ? new ContextPinMoved(from, to, Int(move, "gained"), Int(move, "lost"), Places(move), delta) { RelinkFailed = failed }
                : new ContextPinMoved(from, to, 0, 0, [], delta) { RelinkFailed = failed };
        }, StringComparer.Ordinal);
    }

    // The newest succeeded refresh, when it made this map version and stored at least one move (an entry is what says which two versions were compared).
    private static (JobRow Job, Dictionary<string, JsonElement> Moves)? Stored(IReadOnlyList<JobRow> refreshes, int version)
    {
        var refresh = refreshes.Where(static j => j is { State: JobState.Succeeded, EndedAt: not null, Result.ValueKind: JsonValueKind.Object }).MaxBy(static j => j.EndedAt);
        if (refresh?.Result is not { } result || !Is(result, "version", version) || !result.TryGetProperty("moves", out var moves) || moves.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var stored = moves.EnumerateArray().Where(static m => Text(m, "pin") is not null).ToDictionary(static m => Text(m, "pin")!, StringComparer.Ordinal);   // why: the filter keeps only moves with a pin
        return stored.Count == 0 ? null : (refresh, stored);
    }

    private static List<ContextPinMovedTo> Places(JsonElement move) =>
        move.TryGetProperty("to", out var places) && places.ValueKind == JsonValueKind.Array ? [.. places.EnumerateArray().Select(Place)] : [];

    private static ContextPinMovedTo Place(JsonElement place) =>
        new(Text(place, "id"), Text(place, "name"), Int(place, "features"));

    // The newest succeeded relink of this map version: each pin's records after it minus before it.
    private static Dictionary<string, int>? Counts(IReadOnlyList<JobRow> relinks, int version)
    {
        var relink = relinks.Where(j => j is { State: JobState.Succeeded, Result.ValueKind: JsonValueKind.Object } && Is(j.Result.Value, "mapVersion", version)).MaxBy(static j => j.EndedAt);
        if (relink?.Result is not { } result || !result.TryGetProperty("pins", out var pins) || pins.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return pins.EnumerateArray().Where(static p => Text(p, "id") is not null).ToDictionary(static p => Text(p, "id")!, static p => Int(p, "after") - Int(p, "before"), StringComparer.Ordinal);   // why: the filter keeps only pins with an id
    }

    private static bool Is(JsonElement result, string name, int expected) =>
        result.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) && number == expected;

    private static int Int(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : 0;

    private static string? Text(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
