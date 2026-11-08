namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>Where a workflow run stands (issue 2258, PR 6). The first three are in flight; the last three are final.</summary>
public enum WorkflowRunStatus
{
    /// <summary>Recorded, not started: a 'queue' workflow waits for the run before it.</summary>
    Queued = 1,

    /// <summary>Walking its nodes.</summary>
    Running,

    /// <summary>Waiting on a person, a timer or an agent.</summary>
    Waiting,

    /// <summary>Every node finished or was skipped.</summary>
    Succeeded,

    /// <summary>A node failed and its 'onFailure' was 'stop'.</summary>
    Failed,

    /// <summary>Someone cancelled it.</summary>
    Cancelled,
}
