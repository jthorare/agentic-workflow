using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The updates and the query that let a person and a launched session act on the 'run_agent' node a run waits on (PR 7): takeover, handback and report, each refused by its validator when it does not fit.</summary>
internal sealed partial class GraphWorkflow
{
    private AgentWait? _agent;

    /// <summary>The node the run waits on, or null: the 'agent-node' query.</summary>
    /// <returns>The node, its launch and whether a person holds it.</returns>
    [WorkflowQuery("agent-node")]
    public AgentNodeView? AgentNode() => _agent is { } wait ? new AgentNodeView(wait.Node.Value, wait.ItemIndex, wait.Launch, wait.TakenOver) : null;

    /// <summary>A person takes the node over: its timeout pauses and only the human reports it.</summary>
    /// <param name="call">The node.</param>
    /// <returns>True when this call took it over, false when it already was.</returns>
    [WorkflowUpdate("agent-takeover")]
    public Task<bool> TakeoverAsync(AgentNodeCall call)
    {
        var wait = Waiting(call.Node, call.ItemIndex);
        var changed = !wait.TakenOver;
        wait.TakenOver = true;
        return Task.FromResult(changed);
    }

    /// <summary>Refuses a takeover of a node the run is not waiting on.</summary>
    /// <param name="call">The node.</param>
    [WorkflowUpdateValidator(nameof(TakeoverAsync))]
    public void ValidateTakeover(AgentNodeCall call) => _ = Waiting(call.Node, call.ItemIndex);

    /// <summary>A person hands the node back: its timeout resumes with what was left.</summary>
    /// <param name="call">The node.</param>
    /// <returns>True.</returns>
    [WorkflowUpdate("agent-handback")]
    public Task<bool> HandbackAsync(AgentNodeCall call)
    {
        Waiting(call.Node, call.ItemIndex).TakenOver = false;
        return Task.FromResult(true);
    }

    /// <summary>Refuses a handback of a node nobody holds.</summary>
    /// <param name="call">The node.</param>
    [WorkflowUpdateValidator(nameof(HandbackAsync))]
    public void ValidateHandback(AgentNodeCall call)
    {
        if (!Waiting(call.Node, call.ItemIndex).TakenOver)
        {
            throw Refusal($"node '{call.Node}' is not taken over, so there is nothing to hand back");
        }
    }

    /// <summary>The node's report: it ends with these output values.</summary>
    /// <param name="report">The node and the checked values.</param>
    /// <returns>True.</returns>
    [WorkflowUpdate("agent-report")]
    public Task<bool> ReportAsync(AgentNodeDelivery report)
    {
        Waiting(report.Node, report.ItemIndex).Report = report;
        return Task.FromResult(true);
    }

    /// <summary>Refuses a report for a node the run is not waiting on, a second report, and the launched session's report of a node a person holds.</summary>
    /// <param name="report">The node and the checked values.</param>
    [WorkflowUpdateValidator(nameof(ReportAsync))]
    public void ValidateReport(AgentNodeDelivery report)
    {
        var wait = Waiting(report.Node, report.ItemIndex);
        if (wait.Report is not null)
        {
            throw Refusal($"node '{report.Node}' was already reported");
        }

        if (wait.TakenOver && !report.Human)
        {
            throw Refusal($"node '{report.Node}' is taken over: only the human reports it (fix: report it from a terminal or the Sindri app, or hand it back)");
        }
    }

    private AgentWait Waiting(string node, int itemIndex) =>
        _agent is { } wait && string.Equals(wait.Node.Value, node, StringComparison.Ordinal) && wait.ItemIndex == itemIndex
            ? wait
            : throw Refusal($"the run is not waiting on a run_agent node '{node}'");

    private static ApplicationFailureException Refusal(string message) => new(message, nonRetryable: true);
}
