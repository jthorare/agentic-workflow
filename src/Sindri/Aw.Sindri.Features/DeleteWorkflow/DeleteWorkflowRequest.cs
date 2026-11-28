using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.DeleteWorkflow;

/// <summary>Delete a workflow that is off and has no run in flight: it is archived (it leaves the list for good; its revisions and runs stay readable) and its resources are purged (DELETE /sindri/workflows/{slug}, 'sindri wf delete', MCP workflow_delete).</summary>
/// <param name="Slug">The workflow.</param>
/// <param name="By">Who archives it: 'owner' or 'session:&lt;id&gt;'.</param>
internal sealed record DeleteWorkflowRequest(WorkflowSlug Slug, string By) : ICommand<DeleteWorkflowResponse>;
