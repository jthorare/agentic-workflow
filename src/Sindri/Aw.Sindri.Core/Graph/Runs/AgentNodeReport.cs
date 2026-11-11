using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>A report that passed the run_agent report check, ready for the run's workflow (PR 7).</summary>
/// <param name="Node">The node.</param>
/// <param name="ItemIndex">The item's position, 0 for a node that runs once.</param>
/// <param name="Outputs">The checked values of the declared outputs, as a JSON object.</param>
/// <param name="Human">True for the human's report of a taken-over node; false for the launched session's.</param>
public sealed record AgentNodeReport(NodeId Node, int ItemIndex, string Outputs, bool Human);
