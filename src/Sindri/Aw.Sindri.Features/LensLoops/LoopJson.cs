using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.LensLoops;

/// <summary>Reads the lens loop's facts as JSON trees: they are agent and code output held as text, so a field that is absent is simply absent.</summary>
internal static class LoopJson
{
    /// <summary>A string field, or null when the node is no object, the field is missing or it is not a string.</summary>
    internal static string? Str(JsonNode? node, string name) =>
        node is JsonObject obj && obj[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <summary>The non-null object elements of an array field; none for anything else.</summary>
    internal static IEnumerable<JsonObject> Objects(JsonNode? node, string name) =>
        node is JsonObject obj && obj[name] is JsonArray items ? items.OfType<JsonObject>() : [];

    /// <summary>The fact's JSON text parsed, or null when it is absent; the reason when it is not JSON.</summary>
    internal static (JsonNode? Node, string? Broken) Parse(string? text)
    {
        if (text is null)
        {
            return (null, null);
        }

        try
        {
            return (JsonNode.Parse(text), null);
        }
        catch (JsonException e)
        {
            return (null, e.Message);   // boundary: a fact is text an agent or a person wrote
        }
    }

    /// <summary>The tree as compact UTF-8 JSON text (a writer, so no reflection-based serializer is needed).</summary>
    internal static string Write(JsonNode node)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            node.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
