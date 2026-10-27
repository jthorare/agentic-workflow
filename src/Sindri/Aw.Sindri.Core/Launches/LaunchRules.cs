namespace Aw.Sindri.Core.Launches;

/// <summary>The timing rules of a launch request (UI spec E-1). Pure.</summary>
public static class LaunchRules
{
    /// <summary>How long a request stays fresh: the launcher acts on it, or expires it as SND-LAUNCH-007, within this time.</summary>
    public static TimeSpan Deadline { get; } = TimeSpan.FromSeconds(30);

    /// <summary>How long a session may stay silent before it ends as lost (API spec §8.1).</summary>
    public static TimeSpan LostAfter { get; } = TimeSpan.FromSeconds(120);

    /// <summary>How long a session may sit with its turn ended and no report before it ends as idle; a new prompt starts the clock over.</summary>
    public static TimeSpan IdleAfter { get; } = TimeSpan.FromSeconds(120);

    /// <summary>How long a session may sit idle with no hook saying why (the collector's snapshot alone), or after its one re-prompt, before it is nudged and then ended as idle.</summary>
    public static TimeSpan StalledAfter { get; } = TimeSpan.FromMinutes(10);

    /// <summary>Whether a session whose turn ended at <paramref name="turnEnded"/> has sat idle for <see cref="IdleAfter"/> by <paramref name="now"/>: the rule the idle sweep ends it by and health and Needs you show it by.</summary>
    /// <param name="turnEnded">When the turn ended.</param>
    /// <param name="now">The time now.</param>
    /// <returns>True from <see cref="IdleAfter"/> on.</returns>
    public static bool IdleTurn(DateTimeOffset turnEnded, DateTimeOffset now) => now - turnEnded >= IdleAfter;

    /// <summary>The longest gap between two silence sweeps that still counts as running normally (the sweep runs every 10 s); a longer one means the machine slept.</summary>
    public static TimeSpan SweepGap { get; } = TimeSpan.FromSeconds(60);

    /// <summary>How often a live session's usage is sampled (SCR E-28).</summary>
    public static TimeSpan UsageEvery { get; } = TimeSpan.FromSeconds(60);

    /// <summary>How often 'session wrap' sends a heartbeat (API spec §8.1).</summary>
    public static TimeSpan HeartbeatEvery { get; } = TimeSpan.FromSeconds(30);

    /// <summary>The row a resume addresses among one item's launches (newest first): the newest resumable one, unless a session of that agent still runs (a second agent on it would fight the first).</summary>
    /// <param name="rows">The launches, newest first.</param>
    /// <returns>The row, or null.</returns>
    public static LaunchRow? ResumeCandidate(IEnumerable<LaunchRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var list = rows as IReadOnlyList<LaunchRow> ?? [.. rows];
        var found = list.FirstOrDefault(static row => row.Resumable);
        return found is null || list.Any(row => row.Live && row.Target == found.Target) ? null : found;
    }

    /// <summary>True when <paramref name="row"/> is a request still inside its deadline with no outcome (SND-LAUNCH-008 counts only these).</summary>
    /// <param name="row">The launch.</param>
    /// <param name="now">The time now.</param>
    /// <returns>Whether it is in flight.</returns>
    public static bool InFlight(LaunchRow row, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.State == LaunchState.Requested && now - row.RequestedAt < Deadline;
    }
}
