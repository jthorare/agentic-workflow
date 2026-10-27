using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Launches;

/// <summary>
/// The launcher-owned per-launch files (API spec §8.1, launcher L-9; port, so a slice never touches the disk): the secret's hash, the capture rule
/// and the id the hook reported. The files are deleted with the session, and a start-up sweep removes what a dead launch left.
/// </summary>
public interface ILaunchFiles
{
    /// <summary>Checks a call's secret against the launch's stored hash (a constant-time compare).</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="secret">The secret the call carried.</param>
    /// <returns>Success, or SND-AUTH-003 when the launch has no stored hash (never launched, or ended) or the secret doesn't match.</returns>
    public Result<Unit> Verify(LaunchId launch, LaunchSecret secret);

    /// <summary>When the launch's session last ended a turn: the Stop hook's file, gone again once a new prompt starts a turn.</summary>
    /// <param name="launch">The launch.</param>
    /// <returns>When the turn ended, or null while a turn runs or the session has no such hook.</returns>
    public DateTimeOffset? TurnEndedAt(LaunchId launch);

    /// <summary>When the launch's session last stopped on an API error: the StopFailure hook's file, gone again once a new prompt starts a turn.</summary>
    /// <param name="launch">The launch.</param>
    /// <returns>When the turn failed, or null.</returns>
    public DateTimeOffset? TurnFailedAt(LaunchId launch);

    /// <summary>Whether a tool call is running in the launch's session: the PreToolUse hook's file, deleted when the call returns or fails and when the turn ends.</summary>
    /// <param name="launch">The launch.</param>
    /// <returns>True while the marker exists.</returns>
    public bool ToolRunning(LaunchId launch);

    /// <summary>When the idle sweep sent the launch's session its one re-prompt.</summary>
    /// <param name="launch">The launch.</param>
    /// <returns>When it was sent, or null when it has not been.</returns>
    public DateTimeOffset? NudgedAt(LaunchId launch);

    /// <summary>Records that the launch's session was sent its re-prompt (a launch with no files directory is left alone).</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="at">When it was sent.</param>
    public void MarkNudged(LaunchId launch, DateTimeOffset at);

    /// <summary>Deletes the launch's hash, capture rule and reported-id files; whatever is already gone is fine.</summary>
    /// <param name="launch">The launch.</param>
    public void Forget(LaunchId launch);

    /// <summary>The launches that have a capture rule or a reported-id file on disk.</summary>
    /// <returns>Their ids, each once.</returns>
    public IReadOnlyList<LaunchId> Launches();

    /// <summary>Removes the capture rules and id files older than <paramref name="olderThan"/> whose launch has no live session (the launcher service's start-up sweep).</summary>
    /// <param name="now">The time now.</param>
    /// <param name="olderThan">How old a file must be.</param>
    /// <param name="live">Whether a launch has a live session.</param>
    /// <returns>How many files were removed.</returns>
    public int SweepStale(DateTimeOffset now, TimeSpan olderThan, Func<LaunchId, bool> live);
}
