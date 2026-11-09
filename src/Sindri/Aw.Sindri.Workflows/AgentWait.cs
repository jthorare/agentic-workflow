using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The 'run_agent' node a run waits on (PR 7): which one, who holds it and what was reported. The workflow's updates change it and its loop reads it.</summary>
/// <param name="node">The node.</param>
/// <param name="itemIndex">The item's position, 0 for a node that runs once.</param>
/// <param name="launch">The launch ('LCH-…').</param>
internal sealed class AgentWait(NodeId node, int itemIndex, string launch)
{
    /// <summary>The node.</summary>
    public NodeId Node { get; } = node;

    /// <summary>The item's position, 0 for a node that runs once.</summary>
    public int ItemIndex { get; } = itemIndex;

    /// <summary>The launch ('LCH-…').</summary>
    public string Launch { get; } = launch;

    /// <summary>True from a takeover until the handback: the node's clock stops and only the human reports.</summary>
    public bool TakenOver { get; set; }

    /// <summary>The report the workflow was given, or null.</summary>
    public AgentNodeDelivery? Report { get; set; }
}
