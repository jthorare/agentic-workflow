namespace Aw.Sindri.Features.ListWorkflows;

/// <summary>The workflows, by slug.</summary>
/// <param name="Workflows">One row per workflow; none when nothing was made yet.</param>
internal sealed record ListWorkflowsResponse(IReadOnlyList<WorkflowSummary> Workflows)
{
    /// <summary>The header counts, with Total = Active + Paused + Off. Issue 3683.</summary>
    public WorkflowCounts Counts { get; init; } = new(0, 0, 0, 0);
}
