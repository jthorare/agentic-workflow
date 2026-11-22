using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.RunWorkflow;

/// <summary>Start a run of a workflow by hand (POST /sindri/workflows/{slug}/runs, 'sindri wf run', MCP workflow_run).</summary>
/// <param name="Slug">The workflow.</param>
/// <param name="Payload">What the manual trigger hands on, as a JSON object, or null for none.</param>
/// <param name="By">Who started it: 'owner' or 'session:&lt;id&gt;'.</param>
/// <param name="Revision">A saved revision to run once instead of the live one, or null for the live one (#3691). The live revision does not change.</param>
internal sealed record RunWorkflowRequest(WorkflowSlug Slug, string? Payload, string By, int? Revision = null) : ICommand<RunWorkflowResponse>;
