namespace Aw.Sindri.Core.Events;

/// <summary>The graph engine's event types (issue 2258, PR 6).</summary>
public static partial class EventTypes
{
    /// <summary>A graph run was recorded and its workflow started (issue 2258, PR 6).</summary>
    public const string WorkflowRunStarted = "workflow.run.started";

    /// <summary>A graph run ended, succeeded or failed (issue 2258, PR 6).</summary>
    public const string WorkflowRunFinished = "workflow.run.finished";

    /// <summary>A graph run was cancelled (issue 2258, PR 6).</summary>
    public const string WorkflowRunCancelled = "workflow.run.cancelled";

    /// <summary>A graph node started (issue 2258, PR 6).</summary>
    public const string WorkflowNodeStarted = "workflow.node.started";

    /// <summary>A graph node finished or was skipped (issue 2258, PR 6).</summary>
    public const string WorkflowNodeFinished = "workflow.node.finished";

    /// <summary>The session a 'run_agent' node launched changed state, waiting-on text or last tool (#3151, Agents page E1).</summary>
    public const string WorkflowNodeAgentChanged = "workflow.node.agent.changed";

    /// <summary>A graph node failed (issue 2258, PR 6).</summary>
    public const string WorkflowNodeFailed = "workflow.node.failed";

    /// <summary>A graph 'notify' node posted a note for the owner (issue 2258, PR 6).</summary>
    public const string WorkflowNotify = "workflow.notify";

    /// <summary>A graph 'ask_owner' node asked a question for the owner (issue 2258, PR 9).</summary>
    public const string WorkflowQuestion = "workflow.question";

    /// <summary>An 'ask_owner' node's question was answered, by the owner or by a session (issue 2258, PR 9).</summary>
    public const string WorkflowAnswered = "workflow.answered";

    /// <summary>A person or an agent session took a 'run_agent' node over (#2497).</summary>
    public const string WorkflowNodeTakenOver = "workflow.node.taken-over";

    /// <summary>A person or an agent session handed a taken-over 'run_agent' node back (#2497).</summary>
    public const string WorkflowNodeHandedBack = "workflow.node.handed-back";
}
