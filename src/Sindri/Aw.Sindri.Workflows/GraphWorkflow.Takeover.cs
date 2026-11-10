using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The updates and the query that let a person and a launched session act on the 'run_agent' node a run waits on (PR 7): takeover, handback and report, each refused by its validator when it does not fit.
/// A node inside a 'for_each' body waits in its batch's workflow (#2470): the batch tells the run with the 'agent-child' signal, and the 'agent-node' query then names that child, which takes the updates.
/// </summary>
internal sealed partial class GraphWorkflow
{
    private GraphAgentLoop? _agents;
    private AgentNodeView? _child;

    /// <summary>The node the run waits on, or null: the 'agent-node' query.</summary>
    /// <returns>The node, its launch and whether a person holds it; for a loop item, the child workflow that holds the wait.</returns>
    [WorkflowQuery("agent-node")]
    public AgentNodeView? AgentNode() => _agents?.View() ?? _child;

    /// <summary>A batch of a 'for_each' tells the run that an agent of its body waits (or no longer does).</summary>
    /// <param name="signal">The batch's wait, or none.</param>
    [WorkflowSignal("agent-child")]
    public Task AgentChildAsync(AgentChildSignal signal)
    {
        _child = signal?.View;
        return Task.CompletedTask;
    }

    /// <summary>A person takes the node over: its timeout pauses and only the human reports it.</summary>
    /// <param name="call">The node.</param>
    /// <returns>True when this call took it over, false when it already was.</returns>
    [WorkflowUpdate("agent-takeover")]
    public Task<bool> TakeoverAsync(AgentNodeCall call) => Task.FromResult(Agents.TakeOver(call));

    /// <summary>Refuses a takeover of a node the run is not waiting on.</summary>
    /// <param name="call">The node.</param>
    [WorkflowUpdateValidator(nameof(TakeoverAsync))]
    public void ValidateTakeover(AgentNodeCall call) => Agents.ValidateTakeover(call);

    /// <summary>A person hands the node back: its timeout resumes with what was left.</summary>
    /// <param name="call">The node.</param>
    /// <returns>True.</returns>
    [WorkflowUpdate("agent-handback")]
    public Task<bool> HandbackAsync(AgentNodeCall call)
    {
        Agents.HandBack(call);
        return Task.FromResult(true);
    }

    /// <summary>Refuses a handback of a node nobody holds.</summary>
    /// <param name="call">The node.</param>
    [WorkflowUpdateValidator(nameof(HandbackAsync))]
    public void ValidateHandback(AgentNodeCall call) => Agents.ValidateHandback(call);

    /// <summary>The node's report: it ends with these output values.</summary>
    /// <param name="report">The node and the checked values.</param>
    /// <returns>True.</returns>
    [WorkflowUpdate("agent-report")]
    public Task<bool> ReportAsync(AgentNodeDelivery report)
    {
        Agents.Deliver(report);
        return Task.FromResult(true);
    }

    /// <summary>Refuses a report for a node the run is not waiting on, a second report, and the launched session's report of a node a person holds.</summary>
    /// <param name="report">The node and the checked values.</param>
    [WorkflowUpdateValidator(nameof(ReportAsync))]
    public void ValidateReport(AgentNodeDelivery report) => Agents.ValidateReport(report);

    // why: an update can arrive before RunAsync made the loop; it finds nothing to wait on and is refused
    private GraphAgentLoop Agents => _agents ??= new GraphAgentLoop(_run);
}
