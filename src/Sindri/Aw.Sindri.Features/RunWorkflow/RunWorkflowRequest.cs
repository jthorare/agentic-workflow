using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.RunWorkflow;

/// <summary>Start a run of a workflow by hand (POST /sindri/workflows/{slug}/runs, 'sindri wf run').</summary>
/// <param name="Slug">The workflow.</param>
/// <param name="Payload">What the manual trigger hands on, as a JSON object, or null for none.</param>
/// <param name="By">Who started it: 'owner' or 'session:&lt;id&gt;'.</param>
/// <param name="ExpectRevision">The revision the caller thinks is active, or null to run whatever is active; a different active revision is SND-GRAPH-007.</param>
internal sealed record RunWorkflowRequest(WorkflowSlug Slug, string? Payload, string By, int? ExpectRevision = null) : ICommand<RunWorkflowResponse>;
