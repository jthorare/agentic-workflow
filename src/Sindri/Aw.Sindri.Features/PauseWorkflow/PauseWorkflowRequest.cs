using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.PauseWorkflow;

/// <summary>Turn a workflow's triggers off (POST /sindri/workflows/{slug}/pause, 'sindri wf pause', MCP workflow_pause). Runs in flight are not cancelled.</summary>
/// <param name="Slug">The workflow.</param>
/// <param name="By">Who pauses it: 'owner' or 'session:&lt;id&gt;'.</param>
internal sealed record PauseWorkflowRequest(WorkflowSlug Slug, string By) : ICommand<PauseWorkflowResponse>;
