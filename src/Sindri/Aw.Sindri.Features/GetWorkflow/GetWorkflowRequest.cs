using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.GetWorkflow;

/// <summary>Read one workflow (GET /sindri/workflows/{slug}, 'sindri wf show', MCP workflow_get).</summary>
/// <param name="Slug">The workflow.</param>
/// <param name="Revision">A revision to show instead of the one it runs, or null for that one (?revision=N). A number the workflow has no revision for is SND-GRAPH-008.</param>
/// <param name="Newest">True to show its newest revision (?revision=latest); wins over <paramref name="Revision"/>.</param>
internal sealed record GetWorkflowRequest(WorkflowSlug Slug, int? Revision = null, bool Newest = false) : IQuery<GetWorkflowResponse>;
