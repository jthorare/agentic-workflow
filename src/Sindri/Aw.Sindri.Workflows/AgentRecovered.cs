using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What 'RecoverAgentStep' returns: the report the output folder proves, or why it parks, or neither when the agent wrote nothing.</summary>
/// <param name="Outcome">The outcome to report; null when the run is not completed from its outputs.</param>
/// <param name="Outputs">The stored outputs of that report; empty without an outcome.</param>
/// <param name="Sha">The SHA-256 of the recovered report; null without an outcome.</param>
/// <param name="Reason">Why the run parks (the output folder holds a result that can't be reported on its own); null when it completes or the folder holds nothing.</param>
internal sealed record AgentRecovered(string? Outcome, IReadOnlyDictionary<FactKey, FactValue> Outputs, string? Sha, string? Reason)
{
    /// <summary>The agent left nothing in its output folder: the run fails with the session's own end.</summary>
    internal static AgentRecovered Nothing { get; } = new(Outcome: null, new Dictionary<FactKey, FactValue>(), Sha: null, Reason: null);
}
