using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The 'run_agent' node of a loop body (#2470): this batch holds the wait for its item's agent, tells the run with the 'agent-child' signal, and takes the takeover, handback and report updates
/// that the control sends here once the run's 'agent-node' query has named this workflow.
/// </summary>
internal sealed partial class GraphIterationWorkflow
{
    private GraphAgentLoop? _agents;

    /// <summary>The node this batch waits on, or null: the 'agent-node' query.</summary>
    /// <returns>The node, its launch and whether a person holds it.</returns>
    [WorkflowQuery("agent-node")]
    public AgentNodeView? AgentNode() => _agents?.View();

    /// <summary>A person takes the node over: its timeout pauses and only the human reports it.</summary>
    /// <param name="call">The node.</param>
    /// <returns>True when this call took it over, false when it already was.</returns>
    [WorkflowUpdate("agent-takeover")]
    public Task<bool> TakeoverAsync(AgentNodeCall call) => Task.FromResult(Agents.TakeOver(call));

    /// <summary>Refuses a takeover of a node the batch is not waiting on.</summary>
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

    /// <summary>Refuses a report for a node the batch is not waiting on, a second report, and the launched session's report of a node a person holds.</summary>
    /// <param name="report">The node and the checked values.</param>
    [WorkflowUpdateValidator(nameof(ReportAsync))]
    public void ValidateReport(AgentNodeDelivery report) => Agents.ValidateReport(report);

    private GraphAgentLoop Agents => _agents ??= new GraphAgentLoop(_run, _agentLaunchAttempts, Announce);

    // why: the run's 'agent-node' query names this workflow while an agent of the batch waits, so the control sends the updates here. A failed signal (the run is gone, or a cancel is under way) is not the batch's error
    private async Task Announce(AgentWait? wait)
    {
        if (Workflow.Info.Parent is not { } parent)
        {
            return;
        }

        var view = wait is null ? null : new AgentNodeView(wait.Node.Value, wait.ItemIndex, wait.Launch, wait.TakenOver, Workflow.Info.WorkflowId);
        try
        {
            await Workflow.GetExternalWorkflowHandle(parent.WorkflowId).SignalAsync("agent-child", [new AgentChildSignal(view)]).ConfigureAwait(true);
        }
        catch (Exception e) when (TemporalException.IsCanceledException(e) || e is TemporalException)
        {
            // the run no longer waits for the signal
        }
    }
}
