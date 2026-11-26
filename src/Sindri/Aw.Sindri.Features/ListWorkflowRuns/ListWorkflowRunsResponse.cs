using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.ListWorkflowRuns;

/// <summary>A workflow's newest runs, newest first.</summary>
/// <param name="Slug">The workflow.</param>
/// <param name="Runs">The requested page of runs that pass the filter (at most <see cref="ListWorkflowRunsService.Newest"/> when none is named); none when it never ran or the page is past the end.</param>
/// <param name="More">True when older runs that pass the filter follow this page.</param>
/// <param name="Next">The cursor of the next page while <paramref name="More"/> is true: the last run of this page, to send back as 'before' (#4334); null on the last page.</param>
internal sealed record ListWorkflowRunsResponse(WorkflowSlug Slug, IReadOnlyList<WorkflowRunSummary> Runs, bool More = false, WorkflowRunId? Next = null);
