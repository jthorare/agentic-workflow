using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>The 'run_agent' node a run is waiting on right now (PR 7).</summary>
/// <param name="Node">The node.</param>
/// <param name="ItemIndex">The item's position, 0 for a node that runs once.</param>
/// <param name="Launch">The launch its session runs under.</param>
/// <param name="TakenOver">True while a person holds the node: its timeout is paused and only the human reports it.</param>
public sealed record RunningAgentNode(NodeId Node, int ItemIndex, LaunchId Launch, bool TakenOver);
