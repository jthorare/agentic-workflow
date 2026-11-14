using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Core.Graph.Expressions;

namespace Aw.Sindri.Features.PollConnections;

/// <summary>Decides whether a trigger wants an event, and builds the trigger JSON of the run it starts.</summary>
internal static class TriggerMatch
{
    internal const string Kind = "connection_event";

    /// <summary>Applies a trigger's filter to an event. The condition reads the event's fields as 'trigger.title'; a field the event lacks reads as null. No filter keeps every event; a filter that can't be read keeps none (the validator refuses it when the workflow is saved).</summary>
    /// <param name="filter">The filter text, or null.</param>
    /// <param name="connection">The connection.</param>
    /// <param name="polled">The event.</param>
    /// <returns>True when the trigger takes the event.</returns>
    internal static bool Wants(string? filter, ConnectionName connection, PolledEvent polled)
    {
        ArgumentNullException.ThrowIfNull(polled);
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        return ConditionExpression.TryParse(filter, out var expression, out _) && expression is not null
            && expression.Evaluate(name => Resolve(name, connection, polled));
    }

    /// <summary>The trigger JSON of a run: kind, connection, event, delivery id, time and the event's fields (provider text, untrusted).</summary>
    /// <param name="connection">The connection.</param>
    /// <param name="polled">The event.</param>
    /// <returns>The JSON text.</returns>
    internal static string Trigger(ConnectionName connection, PolledEvent polled)
    {
        ArgumentNullException.ThrowIfNull(polled);
        var json = new JsonObject();
        foreach (var (name, value) in polled.Fields)
        {
            json[name] = Typed(polled.Kind.Key, name, value);
        }

        // why: written last so a provider field named 'event' or 'deliveryId' can't replace the trigger's own
        json["kind"] = Kind;
        json["connection"] = connection.ToString();
        json["event"] = polled.Kind.Key;
        json["deliveryId"] = polled.DeliveryKey;
        json["occurredAt"] = polled.OccurredAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        return json.ToJsonString();
    }

    private static JsonNode? Resolve(string name, ConnectionName connection, PolledEvent polled)
    {
        const string prefix = "trigger.";
        if (!name.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        return name[prefix.Length..] switch
        {
            "connection" => connection.ToString(),
            "event" => polled.Kind.Key,
            "deliveryId" => polled.DeliveryKey,
            "occurredAt" => polled.OccurredAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            var field => polled.Fields.TryGetValue(field, out var value) ? Typed(polled.Kind.Key, field, value) : null,
        };
    }

    // why: a poll keeps every field as text; the kind's schema (EventKindSchemas, #3237) says what a workflow reads. A number is a JSON number, draft a boolean and failing an array.
    // A value that doesn't read as its declared type is null, not text: a workflow then never sees a string where the schema says number.
    private static JsonNode? Typed(string kind, string name, string value)
    {
        if (EventKindSchemas.Find(kind)?.Fields.GetValueOrDefault(name) is not { } type)
        {
            return value;
        }

        return type switch
        {
            "number" => long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null,
            "boolean" => bool.TryParse(value, out var flag) ? flag : null,
            "array" => ArrayOf(value),
            _ => value,
        };
    }

    private static JsonArray? ArrayOf(string value)
    {
        try
        {
            return JsonNode.Parse(value) as JsonArray;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
