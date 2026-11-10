using Aw.Kernel;

namespace Aw.Sindri.Host.Commands;

/// <summary>'sindri toolkit list|create|build|start|stop|pause|resume|calls' (agent-first PR 16c): clients of the toolkit routes.</summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
/// <param name="follower">Follows the create and build jobs.</param>
/// <param name="ensureRunning">Starts the control plane (true: this verb may launch it) or names why not; null: never start it.</param>
internal sealed class ToolkitVerbs(ControlPlaneClient client, Output output, JobFollower follower, Func<bool, CancellationToken, Task<Error?>>? ensureRunning = null)
{
    internal Task<int> Run(string[] args, CancellationToken interrupt, CancellationToken again)
    {
        _ = (client, output, follower, ensureRunning, args, interrupt, again);
        throw new NotSupportedException();
    }
}
