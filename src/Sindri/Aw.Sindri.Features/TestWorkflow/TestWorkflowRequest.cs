using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.TestWorkflow;

/// <summary>Start a test run of a workflow revision (POST /sindri/workflows/{slug}/test, 'sindri wf test', MCP workflow_test). Nothing real is written (Workflows page spec §5).</summary>
/// <param name="Slug">The workflow.</param>
/// <param name="Revision">The revision to test, or null for the newest.</param>
/// <param name="Trigger">A trigger payload as JSON text (a hand edit of an event, or the example), or null.</param>
/// <param name="TriggerEvent">The id of a real event the trigger step saw (see trigger-events), or null.</param>
/// <param name="Pins">Step outputs to use in place of the step, or null for none.</param>
/// <param name="WithModel">True to let 'prompt' steps call the model for real.</param>
/// <param name="WithAgents">True to let 'run_agent' steps start a real agent.</param>
/// <param name="By">Who starts it: 'owner' or 'session:&lt;id&gt;'.</param>
internal sealed record TestWorkflowRequest(WorkflowSlug Slug, int? Revision, string? Trigger, string? TriggerEvent, IReadOnlyList<TestPin>? Pins, bool WithModel, bool WithAgents, string By) : ICommand<TestWorkflowResponse>;
