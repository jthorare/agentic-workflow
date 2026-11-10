using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.ValidateWorkflow;

/// <summary>Check a workflow as if it were being activated (POST /sindri/workflows/validate, 'sindri wf validate --slug', MCP workflow_validate). Give either a stored workflow (<paramref name="Slug"/>, and a revision or the newest) or a graph (<paramref name="Spec"/>). It writes nothing.</summary>
/// <param name="Slug">A stored workflow, or null.</param>
/// <param name="Revision">The revision of that workflow, or null for the newest.</param>
/// <param name="Spec">A graph as JSON text, or null.</param>
internal sealed record ValidateWorkflowRequest(WorkflowSlug? Slug, int? Revision, string? Spec) : ICommand<ValidateWorkflowResponse>;
