using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Launches;

/// <summary>Ends a live launch's session as an exit when the launcher stops it, so the launch stops being live at once and the silence sweep never marks it lost.</summary>
/// <remarks>It reads the projected view, so a launch stopped right after its start was appended (projector behind) is left to the silence sweep. The end reads the final tokens for up to <see cref="LaunchSessionEvents.FinalTokensBudget"/> while the launcher holds the launch's gate.</remarks>
/// <param name="session">The session events: the one 'session.ended' path (its key makes a second end record nothing).</param>
/// <param name="views">The 'launches' view: only a live launch is ended, so a launch that never started or already ended gets no event.</param>
public sealed class LaunchStopEnd(LaunchSessionEvents session, ILaunchViews views)
{
    /// <summary>Ends the launch's session as an exit when its row is live. A ledger that refuses leaves the silence sweep to end it.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="cancellationToken">Cancels the read and the append.</param>
    /// <returns>Done.</returns>
    public async ValueTask EndIfLive(LaunchId launch, CancellationToken cancellationToken)
    {
        var row = await views.Get(launch, cancellationToken).ConfigureAwait(false);
        if (row.IsSuccess && row.Value is { Live: true })
        {
            _ = await session.End(launch, SessionEndReason.Exit, exitCode: null, cancellationToken).ConfigureAwait(false);
        }
    }
}
