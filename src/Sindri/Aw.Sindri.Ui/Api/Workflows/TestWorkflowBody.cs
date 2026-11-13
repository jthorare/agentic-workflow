using System.Text.Json;

namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>The body of POST /sindri/workflows/{slug}/test. Every member is optional; null means the default.</summary>
/// <param name="Revision">The revision to test, or null for the newest.</param>
/// <param name="Trigger">A trigger payload as a JSON object, or null.</param>
/// <param name="TriggerEvent">The id of a real event from the trigger-events list, or null.</param>
/// <param name="Pins">An object of node id to the output to use in place of that step, or null.</param>
/// <param name="WithModel">True to let prompt steps call the model for real.</param>
/// <param name="WithAgents">True to let run_agent steps start a real agent.</param>
internal sealed record TestWorkflowBody(int? Revision, JsonElement? Trigger, string? TriggerEvent, JsonElement? Pins, bool WithModel, bool WithAgents);
