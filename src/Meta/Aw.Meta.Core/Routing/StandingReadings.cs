using Aw.Meta.Core.Usage;

namespace Aw.Meta.Core.Routing;

/// <summary>Folds a provider's own limit signal into the usage look, so a lane whose provider says it is out reads as limited like a Claude lane at its stop.</summary>
public static class StandingReadings
{
    /// <summary>Merges the standings into the readings.</summary>
    /// <param name="readings">The usage look, one per subject.</param>
    /// <param name="standings">The standings built from it, in the same order.</param>
    /// <returns>The readings, each lane with a provider signal reading limited when the provider says so and ok when it does not and nothing else could be read.</returns>
    public static IReadOnlyList<UsageReading> Merge(IReadOnlyList<UsageReading> readings, IReadOnlyList<LaneStanding> standings)
    {
        ArgumentNullException.ThrowIfNull(readings);
        ArgumentNullException.ThrowIfNull(standings);
        var bySubject = standings.ToDictionary(static s => s.Lane, StringComparer.Ordinal);
        return [.. readings.Select(r => bySubject.TryGetValue(r.Subject, out var standing) ? One(r, standing) : r)];
    }

    private static UsageReading One(UsageReading reading, LaneStanding standing)
    {
        if (reading.State == UsageState.Limited || standing.Signal is not { } signal)
        {
            return reading;
        }

        if (!signal.LimitReached)
        {
            return reading.State == UsageState.Unknown ? reading with { State = UsageState.Ok, Reason = null } : reading;
        }

        var full = signal.Windows.Where(static w => w.UsedPct >= 100).ToList();
        var windows = full.Count > 0 ? full : [.. signal.Windows];
        var kind = windows.Exists(static w => string.Equals(w.Name, "weekly", StringComparison.Ordinal)) ? UsageKind.Weekly : UsageKind.Session;
        var stop = new UsageStop(kind, windows.Max(static w => w.ResetsAt), $"{standing.RunningAgent} reports its {kind.ToString().ToLowerInvariant()} limit reached");
        return new UsageReading(reading.Subject, UsageState.Limited, stop, Idle: false, Reason: null, Source: standing.RunningAgent + " rollout");
    }
}
