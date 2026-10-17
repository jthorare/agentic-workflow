using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Launch;

/// <summary>
/// Starts an item's agent CLI in a host and shows it in the configured viewer (API P10a; composed of <see cref="IAgentCatalog"/>,
/// <see cref="ISessionHost"/> and <see cref="ITerminalViewer"/>). The launcher service and engine P8 call it.
/// </summary>
public interface ILauncher
{
    /// <summary>
    /// Launches. Idempotent per launch id: a second call returns the existing <see cref="StartedLaunch"/> with the same
    /// <see cref="HostRef"/> and starts no second session. A failure is a value (<see cref="FailedLaunch"/>), never an exception.
    /// </summary>
    /// <param name="request">The launch.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The outcome.</returns>
    public ValueTask<LaunchResult> Launch(LaunchRequest request, CancellationToken cancellationToken);

    /// <summary>Ends the launch's session and removes its per-launch files. Idempotent: a launch that is gone, or never started, is a success.</summary>
    /// <param name="launch">The launch (the engine's 'LCH-…' id).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or an error when the host could not be asked.</returns>
    public ValueTask<Result<Unit>> Stop(LaunchId launch, CancellationToken cancellationToken);

    /// <summary>Checks the viewer, the host, every catalog target and every repo path.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>What 'launch.probed' records.</returns>
    public ValueTask<LaunchProbed> Probe(CancellationToken cancellationToken);
}
