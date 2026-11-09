namespace Aw.Sindri.Features.ListWorkflows;

/// <summary>The workflows, by slug.</summary>
/// <param name="Workflows">One row per workflow; none when nothing was made yet.</param>
internal sealed record ListWorkflowsResponse(IReadOnlyList<WorkflowSummary> Workflows);
