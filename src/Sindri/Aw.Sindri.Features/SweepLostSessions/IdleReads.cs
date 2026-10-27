using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.SweepLostSessions;

/// <summary>What the idle sweep reads about one Step session: the hooks' marker files and the collector's snapshot, as one <see cref="IdleInputs"/>, and the record that it was sent its re-prompt.</summary>
/// <param name="files">The per-launch files: the turn markers, the tool-running marker and the nudged marker.</param>
/// <param name="snapshot">The collector's latest pass.</param>
internal sealed class IdleReads(ILaunchFiles files, ISessionSnapshot snapshot)
{
    private static readonly TimeSpan _freshSnapshot = TimeSpan.FromMinutes(1);

    /// <summary>What is known about the launch's session at <paramref name="now"/>.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="now">The time now.</param>
    /// <returns>The inputs of <see cref="IdleRules.Decide"/>.</returns>
    public IdleInputs Read(LaunchId launch, DateTimeOffset now) =>
        new(now, files.TurnEndedAt(launch), files.TurnFailedAt(launch), SnapshotIdleSince(launch, now), files.ToolRunning(launch), files.NudgedAt(launch));

    /// <summary>Records that the launch's session was sent its re-prompt.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="now">The time now.</param>
    public void Nudged(LaunchId launch, DateTimeOffset now) => files.MarkNudged(launch, now);

    // When the collector's latest pass saw the launch's session turn idle, from a state it read (Claude's own record or a hook file), never the transcript estimate; null while it works, is unknown or the pass is old.
    private DateTimeOffset? SnapshotIdleSince(LaunchId launch, DateTimeOffset now) =>
        snapshot.Current is { } current && now - current.ObservedAt <= _freshSnapshot
            && current.Sessions.FirstOrDefault(s => s.LaunchId == launch) is { State: SessionState.Idle, Estimated: false } seen
                ? seen.Since
                : null;
}
