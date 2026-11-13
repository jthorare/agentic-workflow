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

    /// <summary>When the last real worktree sweep started (ISO 8601, round-trip); a worktree changed after it is kept ("changed since the last full sweep (this time)"). Absent until a sweep has run.</summary>
    public const string WorktreesSweepStart = "worktrees.sweep-start";

    /// <summary>The prefix of the per-tree sweep marker a <c lang="text">--path</c> sweep records for the one worktree it looked at; <see cref="WorktreesPathSweepStart"/> builds the full key.</summary>
    public const string WorktreesSweepStartPrefix = "worktrees.sweep-start.";

    /// <summary>The key holding when a <c lang="text">--path</c> sweep of one worktree last started (ISO 8601, round-trip).</summary>
    /// <param name="path">The worktree path, already normalized by the caller with <c lang="C#">WorktreeRoots.Normalize</c> (the root namespace may not depend on Worktrees: ARCH-CYCLE-001, #3422).</param>
    /// <returns>The key.</returns>
    public static string WorktreesPathSweepStart(string path) => WorktreesSweepStartPrefix + path;

    /// <summary>Pull requests the watch saw end whose worktrees it could not yet sweep (the sweep could not run), comma separated; retried on each poll.</summary>
    public const string WorktreesPending = "worktrees.pending-prs";

    /// <summary>The disk and memory state the last watch reported ("disk=low mem=ok"), so a re-armed watch does not announce an unchanged state again.</summary>
    public const string WatchResources = "watch.resources";

    /// <summary>The board: main's latest move as "version|sha|ticks|pr,pr" (the version goes up on each move).</summary>
    public const string BoardMain = "board.main";

    /// <summary>The prefix of a lane's last-seen mark of the board, "board.seen.&lt;lane&gt;", holding "boardVersion,laneMapVersion".</summary>
    public const string BoardSeenPrefix = "board.seen.";

    /// <summary>The lanes the last watch reported CONTEXT HIGH for (comma-separated), so a re-armed watch does not announce a standing one again.</summary>
    public const string WatchContext = "watch.context";

    /// <summary>The lanes the last watch saw with a live session and the ones it reported LANE DOWN for (LaneWatch, "up=a,b;down=c"), so a re-armed watch neither repeats a standing one nor misses one that went down between runs (#2856).</summary>
    public const string WatchLanes = "watch.lanes";

    /// <summary>The status lines a watch announced and when its last STATUS line printed (StatusMemory), so the next watch run neither repeats nor drops one (#2651).</summary>
    public const string WatchStatus = "watch.status";

    /// <summary>The pull requests and main's sha the last watch poll settled on (WatchMemory), so the next watch run diffs its first poll against them instead of seeding silently.</summary>
    public const string WatchSnapshot = "watch.snapshot";

    /// <summary>"1" while the watch's polls are failing (set by the first failed poll, cleared by a good one): a repeat error in a later run is not a new event.</summary>
    public const string WatchFailing = "watch.failing";

    /// <summary>The pid of the newest watch (#2564). Only that watch keeps watch.snapshot and watch.failing; an older one prints WATCH superseded and ends.</summary>
    public const string WatchOwner = "watch.owner";

    /// <summary>When the owner or meta dropped a pull request's merge-queue entry (aw-meta queue-drop), as "pr=ticks" pairs; a ready message newer than the drop queues it again.</summary>
    public const string QueueDropped = "queue.dropped";

    /// <summary>The pull requests merge-run handed to GitHub's merge queue that the watch has not seen end yet, as "pr=ticks=head" triples (MergeQueued).</summary>
    public const string MergeQueued = "merge.queued";

    /// <summary>The running (or last) merge-train's summary lines, as the status shows them.</summary>
    public const string Train = "train.state";

    /// <summary>The usage-limit stops the watch has announced (so a re-armed watch does not repeat them).</summary>
    public const string UsageWatch = "usage.watch";

    /// <summary>The route suggestions the watch has announced (so a re-armed watch does not repeat them).</summary>
    public const string RouteWatch = "route.watch";

    /// <summary>Today's report-defect counts as "yyyy-MM-dd|filed|skipped" (the UTC date); another date means 0|0.</summary>
    public const string DefectsDay = "defects.day";
}
