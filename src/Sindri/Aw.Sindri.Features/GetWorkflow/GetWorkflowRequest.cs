using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.GetWorkflow;

/// <summary>Read one workflow (GET /sindri/workflows/{slug}, 'sindri wf show', MCP workflow_get).</summary>
/// <param name="Slug">The workflow.</param>
internal sealed record GetWorkflowRequest(WorkflowSlug Slug) : IQuery<GetWorkflowResponse>;
