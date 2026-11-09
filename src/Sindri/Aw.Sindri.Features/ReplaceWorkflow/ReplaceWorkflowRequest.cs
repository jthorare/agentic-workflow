using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.ReplaceWorkflow;

/// <summary>Replace a workflow's whole graph with a new revision (PUT /sindri/workflows/{slug}, 'sindri wf edit --file', MCP workflow_replace).</summary>
/// <param name="Slug">The workflow.</param>
/// <param name="BaseRevision">The latest revision the caller read; a newer one is SND-GRAPH-007 with the current revision.</param>
/// <param name="Spec">The whole graph as JSON text.</param>
/// <param name="By">Who replaces it: 'owner' or 'session:&lt;id&gt;'.</param>
internal sealed record ReplaceWorkflowRequest(WorkflowSlug Slug, int BaseRevision, string Spec, string By) : ICommand<ReplaceWorkflowResponse>;
