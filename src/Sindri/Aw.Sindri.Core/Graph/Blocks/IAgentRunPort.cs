using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>Launches the session of a 'run_agent' node and waits for its report (a port so Core holds no Temporal or launcher types).</summary>
public interface IAgentRunPort
{
    /// <summary>Runs one session to its report.</summary>
    /// <param name="request">The node execution.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The report, or why the session ended without one ('exit', 'lost', 'idle' or 'timeout').</returns>
    public ValueTask<Result<AgentRunReport>> Run(AgentRunRequest request, CancellationToken cancellationToken);
}
