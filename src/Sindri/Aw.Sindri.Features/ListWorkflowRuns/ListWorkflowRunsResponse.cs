using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.ListWorkflowRuns;

/// <summary>A workflow's newest runs, newest first.</summary>
/// <param name="Slug">The workflow.</param>
/// <param name="Runs">The requested page of runs of every status (at most <see cref="ListWorkflowRunsService.Newest"/> when none is named); none when it never ran or the page is past the end.</param>
/// <param name="More">True when older runs follow this page.</param>
internal sealed record ListWorkflowRunsResponse(WorkflowSlug Slug, IReadOnlyList<WorkflowRunSummary> Runs, bool More = false);
