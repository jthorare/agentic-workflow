namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What 'RecoverAgentNode' returns (PR 7): the report the output folder proves, or why it proves none.</summary>
/// <param name="Outputs">The checked values of the declared outputs as a JSON object; null when the folder does not prove the node finished.</param>
/// <param name="Reason">Why not, in the words a person reads; null when <paramref name="Outputs"/> is set or the folder held nothing.</param>
internal sealed record AgentNodeRecovered(string? Outputs, string? Reason);
