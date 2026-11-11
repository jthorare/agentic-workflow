using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.CreateWorkflow;

/// <summary>Make a workflow from a spec or a starter template (POST /sindri/workflows, 'sindri wf create', MCP workflow_create). Give exactly one of <paramref name="Template"/> and <paramref name="Spec"/>.</summary>
/// <param name="Slug">The new workflow's slug.</param>
/// <param name="Template">A starter template's name, or null.</param>
/// <param name="Spec">The graph as JSON text, or null.</param>
/// <param name="By">Who creates it: 'owner' or 'session:&lt;id&gt;'.</param>
internal sealed record CreateWorkflowRequest(WorkflowSlug Slug, string? Template, string? Spec, string By) : ICommand<CreateWorkflowResponse>;
