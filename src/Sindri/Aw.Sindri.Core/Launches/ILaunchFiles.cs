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
