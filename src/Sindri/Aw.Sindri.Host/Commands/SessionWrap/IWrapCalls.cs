using Aw.Kernel;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>The two calls 'session wrap' makes to the control plane (API spec §8.1), each proven by the launch's secret.</summary>
internal interface IWrapCalls
{
    /// <summary>Says the session is alive.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="proof">The launch secret.</param>
    /// <param name="session">The agent's session id once found, otherwise null.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or why the control plane refused or was unreachable.</returns>
    public ValueTask<Result<Unit>> Heartbeat(LaunchId launch, LaunchSecret proof, SessionId? session, CancellationToken cancellationToken);

    /// <summary>Says the agent exited.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="proof">The launch secret.</param>
    /// <param name="exitCode">The agent's exit code.</param>
    /// <param name="session">The agent's session id if found, otherwise null.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or why the control plane refused or was unreachable.</returns>
    public ValueTask<Result<Unit>> Exit(LaunchId launch, LaunchSecret proof, int exitCode, SessionId? session, CancellationToken cancellationToken);
}
