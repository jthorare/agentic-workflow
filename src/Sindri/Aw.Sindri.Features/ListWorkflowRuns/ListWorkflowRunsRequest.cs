using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.ListWorkflowRuns;

/// <summary>List a workflow's newest runs (GET /sindri/workflows/{slug}/runs, 'sindri wf runs', MCP workflow_runs).</summary>
/// <param name="Slug">The workflow.</param>
internal sealed record ListWorkflowRunsRequest(WorkflowSlug Slug) : IQuery<ListWorkflowRunsResponse>;
