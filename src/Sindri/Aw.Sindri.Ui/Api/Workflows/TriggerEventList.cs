using System.Text.Json;

namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>GET /sindri/workflows/trigger-events: the last events of a kind, newest first.</summary>
/// <param name="Kind">The kind asked for.</param>
/// <param name="Events">The events.</param>
/// <param name="Example">The kind's sample payload, or null; kept raw.</param>
internal sealed record TriggerEventList(string Kind, IReadOnlyList<TriggerEventView> Events, JsonElement? Example);
