namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>How the wait for a 'run_agent' node ended (PR 7): the checked outputs of a report or a recovery, or why the node failed.</summary>
/// <param name="Outputs">The checked values of the declared outputs as a JSON object; null when the node failed.</param>
/// <param name="Failure">Why the node failed, led by its reason ('exit', 'lost', 'idle', 'limit', 'timeout'); null when it completed.</param>
/// <param name="FailureOutput">What the agent produced before the node failed, as the JSON the failed node run stores (its last refused report, its declared output files); null when it left nothing.</param>
internal sealed record AgentNodeEnding(string? Outputs, string? Failure, string? FailureOutput = null);
