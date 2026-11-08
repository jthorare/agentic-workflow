namespace Aw.Meta.Core;

/// <summary>The keys the meta keeps in its key-value store.</summary>
public static class KvKeys
{
    /// <summary>When the watch last polled (ISO 8601); absent until a watch has run.</summary>
    public const string WatchHeartbeat = "watch.heartbeat";

    /// <summary>The stop-check loop guard's last open set.</summary>
    public const string StopFingerprint = "stopcheck.fingerprint";

    /// <summary>How many times in a row that set blocked.</summary>
    public const string StopCount = "stopcheck.count";

    /// <summary>The watch's poll interval in seconds, so a stale heartbeat is measured against it.</summary>
    public const string WatchInterval = "watch.interval";

    /// <summary>The last health check's one-line summary.</summary>
    public const string HealthLast = "health.last";

    /// <summary>The pull requests the watch saw merged or closed on GitHub, comma separated; the merge queue leaves them out.</summary>
    public const string EndedPrs = "watch.ended-prs";

    /// <summary>The last worktree sweep's one-line summary (a trigger's or an explicit sweep's).</summary>
    public const string WorktreesLast = "worktrees.last";

    /// <summary>When the last real worktree sweep started (ISO 8601, round-trip); a worktree changed after it is kept ("changed since the last sweep"). Absent until a sweep has run.</summary>
    public const string WorktreesSweepStart = "worktrees.sweep-start";

    /// <summary>Pull requests the watch saw end whose worktrees it could not yet sweep (the sweep could not run), comma separated; retried on each poll.</summary>
    public const string WorktreesPending = "worktrees.pending-prs";

    /// <summary>The disk and memory state the last watch reported ("disk=low mem=ok"), so a re-armed watch does not announce an unchanged state again.</summary>
    public const string WatchResources = "watch.resources";

    /// <summary>The lanes the last watch reported CONTEXT HIGH for (comma-separated), so a re-armed watch does not announce a standing one again.</summary>
    public const string WatchContext = "watch.context";

    /// <summary>When the owner or meta dropped a pull request's merge-queue entry (aw-meta queue-drop), as "pr=ticks" pairs; a ready message newer than the drop queues it again.</summary>
    public const string QueueDropped = "queue.dropped";

    /// <summary>The running (or last) merge-train's summary lines, as the status shows them.</summary>
    public const string Train = "train.state";

    /// <summary>The usage-limit stops the watch has announced (so a re-armed watch does not repeat them).</summary>
    public const string UsageWatch = "usage.watch";

    /// <summary>The route suggestions the watch has announced (so a re-armed watch does not repeat them).</summary>
    public const string RouteWatch = "route.watch";
}
