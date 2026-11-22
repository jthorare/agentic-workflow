using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.RunWorkflow;

/// <summary>Start a run of a workflow by hand (POST /sindri/workflows/{slug}/runs, 'sindri wf run').</summary>
/// <param name="Slug">The workflow.</param>
/// <param name="Payload">What the manual trigger hands on, as a JSON object, or null for none.</param>
/// <param name="By">Who started it: the caller's actor ('human:cli', 'human:ui', 'session:/// <param name="By">Who started it: 'owner' or 'session:&lt;id&gt;'.</param>lt;label/// <param name="By">Who started it: 'owner' or 'session:&lt;id&gt;'.</param>gt;', 'session:launch:/// <param name="By">Who started it: 'owner' or 'session:&lt;id&gt;'.</param>lt;id/// <param name="By">Who started it: 'owner' or 'session:&lt;id&gt;'.</param>gt;', 'service:/// <param name="By">Who started it: 'owner' or 'session:&lt;id&gt;'.</param>lt;name/// <param name="By">Who started it: 'owner' or 'session:&lt;id&gt;'.</param>gt;'), or 'owner' when no caller is set.</param>
internal sealed record RunWorkflowRequest(WorkflowSlug Slug, string? Payload, string By) : ICommand<RunWorkflowResponse>;
