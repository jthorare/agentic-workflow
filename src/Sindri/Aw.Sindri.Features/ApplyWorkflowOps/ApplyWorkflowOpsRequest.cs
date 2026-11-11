using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.ApplyWorkflowOps;

/// <summary>Edit a workflow with small ops against the revision the caller read (POST /sindri/workflows/{slug}/ops, 'sindri wf edit', MCP workflow_apply_ops).</summary>
/// <param name="Slug">The workflow.</param>
/// <param name="BaseRevision">The latest revision the caller read; a newer one is SND-GRAPH-007 with the current revision.</param>
/// <param name="Ops">The ops as a JSON array of objects with an 'op' member (add_node, set_config, remove_node, connect, disconnect, rename, set_meta).</param>
/// <param name="By">Who edits: 'owner' or 'session:&lt;id&gt;'.</param>
internal sealed record ApplyWorkflowOpsRequest(WorkflowSlug Slug, int BaseRevision, string Ops, string By) : ICommand<ApplyWorkflowOpsResponse>;
