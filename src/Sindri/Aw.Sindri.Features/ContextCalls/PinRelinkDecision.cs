using System.Text.Json;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// Whether the 'context-map-refresh' tick queues a RelinkPinAliases job (concept pins spec 3.7): there is a current map, no refresh, publish or relink runs, the pair (aliases hash of the approved pins, current map
/// version) differs from the one in the result of the newest succeeded relink (or none succeeded yet), and the wait after failed relinks (the same 5, 10, 20, 40, 60 minutes the other kinds use) is over.
/// </summary>
internal static class PinRelinkDecision
{
    private const int RecentRuns = 8;

    /// <param name="currentMap">The current map version, or null when there is none.</param>
    /// <param name="aliasesHash">The hash of the approved pins' aliases now.</param>
    /// <param name="relinks">Every relink job.</param>
    /// <param name="others">Every refresh and publish job.</param>
    /// <param name="now">The time.</param>
    internal static bool ShouldStart(int? currentMap, string aliasesHash, IReadOnlyList<JobRow> relinks, IEnumerable<JobRow> others, DateTimeOffset now)
    {
        if (currentMap is not { } version || relinks.Concat(others).Any(static j => j.State == JobState.Running))
        {
            return false;
        }

        return !Linked(relinks, aliasesHash, version) && !Waiting(relinks, now);
    }

    // The newest succeeded relink stored the pair the tick would queue for.
    private static bool Linked(IReadOnlyList<JobRow> relinks, string aliasesHash, int version)
    {
        var stored = relinks.Where(static j => j.State == JobState.Succeeded && j.Result is { ValueKind: JsonValueKind.Object })
            .OrderByDescending(static j => j.EndedAt).FirstOrDefault()?.Result;
        return stored is { } result && Is(result, "aliasesHash", aliasesHash) && result.TryGetProperty("mapVersion", out var mapVersion) && mapVersion.TryGetInt32(out var linked) && linked == version;
    }

    private static bool Waiting(IReadOnlyList<JobRow> relinks, DateTimeOffset now)
    {
        JobRow[] finished = [.. relinks.Where(static j => j.State != JobState.Running && j.EndedAt is not null).OrderByDescending(static j => j.EndedAt).Take(RecentRuns)];
        var failures = MainMoveDecision.FailedInARow([.. finished.Select(static j => new SyncAttempt(j.State == JobState.Failed, j.EndedAt ?? default))]);
        return failures > 0 && now < finished[0].EndedAt + SyncGapDecision.Wait(failures);
    }

    private static bool Is(JsonElement result, string name, string expected) =>
        result.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && string.Equals(value.GetString(), expected, StringComparison.Ordinal);
}
