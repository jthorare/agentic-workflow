using System.Text.Json.Nodes;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/workflows/{slug}/test. Every member is optional.</summary>
/// <param name="Revision">The revision to test, or null for the newest.</param>
/// <param name="Trigger">A trigger payload as a JSON object (a hand edit of an event, or the example), or null.</param>
/// <param name="TriggerEvent">The id of a real event from GET /sindri/workflows/trigger-events, or null.</param>
/// <param name="Pins">Step outputs to use in place of the step: an object of node id to output, or null.</param>
/// <param name="WithModel">True to let 'prompt' steps call the model for real.</param>
/// <param name="WithAgents">True to let 'run_agent' steps start a real agent.</param>
internal sealed record TestWorkflowBody(int? Revision = null, JsonNode? Trigger = null, string? TriggerEvent = null, JsonObject? Pins = null, bool WithModel = false, bool WithAgents = false);
