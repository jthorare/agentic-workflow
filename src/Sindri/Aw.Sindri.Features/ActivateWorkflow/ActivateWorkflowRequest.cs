using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.ActivateWorkflow;

/// <summary>Turn a workflow's triggers on at one revision (POST /sindri/workflows/{slug}/activate, 'sindri wf activate', MCP workflow_activate).</summary>
/// <param name="Slug">The workflow.</param>
/// <param name="Revision">The revision to run, or null for the newest.</param>
/// <param name="By">Who activates it: 'owner' or 'session:&lt;id&gt;'.</param>
/// <param name="IfLatest">True to refuse when a newer revision than <paramref name="Revision"/> exists (SND-GRAPH-007). Carried; the P4b phase enforces it.</param>
/// <param name="UntestedOk">True to activate a revision that has no passing test run. Carried; the P4b phase enforces the check it lifts.</param>
internal sealed record ActivateWorkflowRequest(WorkflowSlug Slug, int? Revision, string By, bool IfLatest = false, bool UntestedOk = false) : ICommand<ActivateWorkflowResponse>;
