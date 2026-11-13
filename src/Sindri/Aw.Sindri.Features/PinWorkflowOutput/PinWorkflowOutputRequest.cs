using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.PinWorkflowOutput;

/// <summary>Pin a step's output so test runs use it in place of the step (POST /sindri/workflows/{slug}/pins, 'sindri wf pin', MCP workflow_pin). Give the output as text, or a run to take it from.</summary>
/// <param name="Slug">The workflow.</param>
/// <param name="Revision">The revision the pin is taken on, or null for the newest.</param>
/// <param name="Node">The step's node id.</param>
/// <param name="Run">The run to copy the step's output from, or null.</param>
/// <param name="Output">The output as JSON text, or null when <paramref name="Run"/> is given.</param>
/// <param name="By">Who pins it: 'owner' or 'session:&lt;id&gt;'.</param>
internal sealed record PinWorkflowOutputRequest(WorkflowSlug Slug, int? Revision, string Node, WorkflowRunId? Run, string? Output, string By) : ICommand<PinWorkflowOutputResponse>;
