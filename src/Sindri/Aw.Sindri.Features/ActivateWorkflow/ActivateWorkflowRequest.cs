using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.ActivateWorkflow;

/// <summary>Turn a workflow's triggers on at one revision (POST /sindri/workflows/{slug}/activate, 'sindri wf activate', MCP workflow_activate).</summary>
/// <param name="Slug">The workflow.</param>
/// <param name="Revision">The revision to run, or null for the newest.</param>
/// <param name="By">Who activates it: 'owner' or 'session:&lt;id&gt;'.</param>
internal sealed record ActivateWorkflowRequest(WorkflowSlug Slug, int? Revision, string By) : ICommand<ActivateWorkflowResponse>;
