using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.ListWorkflowRuns;

/// <summary>List a workflow's newest runs (GET /sindri/workflows/{slug}/runs, 'sindri wf runs', MCP workflow_runs).</summary>
/// <param name="Slug">The workflow.</param>
/// <param name="Offset">How many of the newest runs to skip (0 when the call pages no further).</param>
/// <param name="Limit">How many runs to return, at most; <see cref="ListWorkflowRunsService.Newest"/> when the call names none.</param>
internal sealed record ListWorkflowRunsRequest(WorkflowSlug Slug, int Offset = 0, int Limit = ListWorkflowRunsService.Newest) : IQuery<ListWorkflowRunsResponse>;
