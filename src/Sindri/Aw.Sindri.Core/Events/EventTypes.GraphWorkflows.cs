namespace Aw.Sindri.Core.Events;

/// <summary>The graph workflow lifecycle event types (issue 2258, PR 8).</summary>
public static partial class EventTypes
{
    /// <summary>A workflow was made, with its first revision (issue 2258, PR 8).</summary>
    public const string WorkflowCreated = "workflow.created";

    /// <summary>A workflow got a new revision, from an edit or a replace (issue 2258, PR 8).</summary>
    public const string WorkflowRevised = "workflow.revised";

    /// <summary>A workflow's active revision was set, so its triggers run (issue 2258, PR 8).</summary>
    public const string WorkflowActivated = "workflow.activated";

    /// <summary>A workflow was paused: no revision is active (issue 2258, PR 8).</summary>
    public const string WorkflowPaused = "workflow.paused";

    /// <summary>A workflow was archived (issue 2258, PR 8).</summary>
    public const string WorkflowArchived = "workflow.archived";
}
