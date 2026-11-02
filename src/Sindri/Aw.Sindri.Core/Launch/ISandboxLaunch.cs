using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Launch;

/// <summary>
/// The launcher's sandbox step (sandbox spec §12 SB-5a): plan, probe, cap, credentials, egress, self-check and wrap for one unattended launch, and the
/// release of what that left behind (egress, session home, denials) when the session ends.
/// </summary>
public interface ISandboxLaunch
{
    /// <summary>Confines one launch: on success the launcher runs <see cref="PreparedSandbox.Argv"/> instead of the argv it built.</summary>
    /// <param name="request">The launch.</param>
    /// <param name="argv">The agent's argv as the launcher built it, unconfined.</param>
    /// <param name="chosenSession">The session id the launcher chose for the agent (an entry in 'pre-chosen-uuid' mode, or a resumed session), or null when the agent reports its own later; the denial collector finds the launch's transcript with it.</param>
    /// <param name="cancellationToken">Cancels the preparation.</param>
    /// <returns>The prepared sandbox, or the SND-LAUNCH-014..017 or -020 refusal (<see cref="SandboxCodes"/>).</returns>
    public ValueTask<Result<PreparedSandbox>> Prepare(LaunchRequest request, IReadOnlyList<string> argv, SessionId? chosenSession, CancellationToken cancellationToken);

    /// <summary>Releases one launch's sandbox: collects its denials, stops its egress and removes its session home. A launch with nothing prepared is a no-op.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="cancellationToken">Cancels the release.</param>
    /// <returns>A task that completes when the launch's sandbox is gone.</returns>
    public ValueTask Release(LaunchId launch, CancellationToken cancellationToken);
}
