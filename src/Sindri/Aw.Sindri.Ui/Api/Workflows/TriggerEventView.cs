using System.Text.Json;

namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>One real event a trigger saw.</summary>
/// <param name="Id">The event's id; send it as triggerEvent to a test.</param>
/// <param name="OccurredAt">When it happened.</param>
/// <param name="Summary">One line about it.</param>
/// <param name="Payload">The event's payload; kept raw.</param>
/// <param name="WouldMatch">True when the trigger's filter would start a run for it; null when no trigger config was given.</param>
internal sealed record TriggerEventView(string Id, DateTimeOffset OccurredAt, string Summary, JsonElement? Payload, bool? WouldMatch);
