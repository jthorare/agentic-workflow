namespace Aw.Sindri.Core.Sessions;

/// <summary>
/// What <see cref="AgentReasons.Evaluate"/> reads for one Agents row (Agents page spec §8, Reasons): the session's state, the lane's context verdict, usage state, queue entries,
/// obligations, health text and heavy wait, and the two thresholds. The collector fills it (A1); the function does no reading of its own.
/// </summary>
/// <param name="Now">The time of the read.</param>
/// <param name="Name">What the row's words call it: the lane name, or the title of a session with no lane.</param>
/// <param name="StateSince">When the row's state began (the hook's or record's time).</param>
public sealed record AgentReasonInputs(DateTimeOffset Now, string Name, DateTimeOffset StateSince)
{
    /// <summary>The row's Meta lane; null for a session or a job with no lane.</summary>
    public string? Lane { get; init; }

    /// <summary>True for the meta lane, the only lane whose open obligations count as owed work and whose open RecycleSelf obligation counts as the recycle mark.</summary>
    public bool IsMetaLane { get; init; }

    /// <summary>True for a Claude lane. A lane that is not Claude can owe undelivered mail (spec §3.2, 3c).</summary>
    public bool IsClaudeLane { get; init; } = true;

    /// <summary>False for a lane in the team map with no live session.</summary>
    public bool HasLiveSession { get; init; } = true;

    /// <summary>True when the owner stopped the lane from the page (Stop or a recycle): it shows as stopped, not as a lane with no session.</summary>
    public bool StoppedFromPage { get; init; }

    /// <summary>The session's state from the hook or the record.</summary>
    public SessionState State { get; init; } = SessionState.Running;

    /// <summary>True when the state is a guess from the transcript's age.</summary>
    public bool Estimated { get; init; }

    /// <summary>Where the state came from. It is the source of the Needs-you reasons of class 1.</summary>
    public AgentStateSource StateSource { get; init; } = AgentStateSource.Hook;

    /// <summary>Why and since when the session asks, when its state is asking.</summary>
    public SessionAsking? Asking { get; init; }

    /// <summary>The lane's context in tokens, when known.</summary>
    public int? ContextTokens { get; init; }

    /// <summary>True when <c lang="text">aw-meta lanes-context</c> says Recycle for the lane (its context is at or over <c lang="text">context.recycleAt</c>).</summary>
    public bool AtRecycleMark { get; init; }

    /// <summary>When the lane crossed the recycle mark, from the source. Null: the time of the read.</summary>
    public DateTimeOffset? RecycleSince { get; init; }

    /// <summary>True when the meta lane has an open RecycleSelf obligation.</summary>
    public bool RecycleSelfOpen { get; init; }

    /// <summary>True when <c lang="text">aw-meta usage</c> lists the lane as Limited.</summary>
    public bool UsageLimited { get; init; }

    /// <summary>When the limit resets; null when the text gave no time.</summary>
    public DateTimeOffset? UsageResetsAt { get; init; }

    /// <summary>When the lane became limited. Null: the time of the read.</summary>
    public DateTimeOffset? UsageSince { get; init; }

    /// <summary>The PRs in the merge queue that this lane sent.</summary>
    public IReadOnlyList<AgentQueuedPr> QueuedPrs { get; init; } = [];

    /// <summary>How many open Reply or RelayOwner obligations the meta lane has.</summary>
    public int OpenObligations { get; init; }

    /// <summary>How many minutes mail has waited undelivered to a lane that is not Claude, from <c lang="text">aw-meta health</c>; null when health reports none.</summary>
    public int? MailUndeliveredMinutes { get; init; }

    /// <summary>The wait for a heavy slot, when this row is the waiter.</summary>
    public AgentHeavyWait? HeavyWait { get; init; }

    /// <summary>The minutes a lane idles before owed work lists it (<c lang="text">agents.idleOwedMinutes</c>, default 10; the profile's default is <c lang="text">AgentSettings.DefaultIdleOwedMinutes</c>).</summary>
    public int IdleOwedMinutes { get; init; } = 10;

    /// <summary>The minutes a heavy waiter waits before it is listed (<c lang="text">agents.slotWaitMinutes</c>).</summary>
    public int SlotWaitMinutes { get; init; } = 30;
}
