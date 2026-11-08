using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>The lowercase text of a run or node-run status as stored.</summary>
internal static class RunStatusText
{
    public static string Of(WorkflowRunStatus status) => status switch
    {
        WorkflowRunStatus.Queued => "queued",
        WorkflowRunStatus.Running => "running",
        WorkflowRunStatus.Waiting => "waiting",
        WorkflowRunStatus.Succeeded => "succeeded",
        WorkflowRunStatus.Failed => "failed",
        WorkflowRunStatus.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    public static string Of(NodeRunStatus status) => status switch
    {
        NodeRunStatus.Running => "running",
        NodeRunStatus.Waiting => "waiting",
        NodeRunStatus.Succeeded => "succeeded",
        NodeRunStatus.Failed => "failed",
        NodeRunStatus.Skipped => "skipped",
        NodeRunStatus.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    public static WorkflowRunStatus Run(string text) => text switch
    {
        "queued" => WorkflowRunStatus.Queued,
        "running" => WorkflowRunStatus.Running,
        "waiting" => WorkflowRunStatus.Waiting,
        "succeeded" => WorkflowRunStatus.Succeeded,
        "failed" => WorkflowRunStatus.Failed,
        "cancelled" => WorkflowRunStatus.Cancelled,
        _ => throw new InvalidDataException($"unknown run status '{text}'"),
    };

    public static NodeRunStatus Node(string text) => text switch
    {
        "running" => NodeRunStatus.Running,
        "waiting" => NodeRunStatus.Waiting,
        "succeeded" => NodeRunStatus.Succeeded,
        "failed" => NodeRunStatus.Failed,
        "skipped" => NodeRunStatus.Skipped,
        "cancelled" => NodeRunStatus.Cancelled,
        _ => throw new InvalidDataException($"unknown node run status '{text}'"),
    };

    /// <summary>True for a status after which the run no longer changes.</summary>
    public static bool IsFinal(WorkflowRunStatus status) => status is WorkflowRunStatus.Succeeded or WorkflowRunStatus.Failed or WorkflowRunStatus.Cancelled;
}
