using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.ListWorkflowRuns;

/// <summary>A workflow's newest runs, newest first.</summary>
/// <param name="Slug">The workflow.</param>
/// <param name="Runs">At most <see cref="ListWorkflowRunsService.Newest"/> runs of every status; none when it never ran.</param>
internal sealed record ListWorkflowRunsResponse(WorkflowSlug Slug, IReadOnlyList<WorkflowRunSummary> Runs);
