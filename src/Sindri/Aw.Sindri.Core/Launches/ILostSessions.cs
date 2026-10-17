namespace Aw.Sindri.Core.Launches;

/// <summary>Ends the sessions that went silent (API spec §8.1): one 'session.ended' with reason lost per launch without a heartbeat for <see cref="LaunchRules.LostAfter"/>.</summary>
public interface ILostSessions
{
    /// <summary>Checks the silent launches once.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How many sessions it ended.</returns>
    public ValueTask<int> Sweep(CancellationToken cancellationToken);

    /// <summary>Removes the per-launch files older than <paramref name="olderThan"/> that belong to no live session (the launcher service's start-up sweep). A launch is live when the 'launches' view says so.</summary>
    /// <param name="olderThan">How old a file must be.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How many files were removed, or null when the views are not caught up yet (a session that is live may not show; try again).</returns>
    public ValueTask<int?> SweepFiles(TimeSpan olderThan, CancellationToken cancellationToken);
}
