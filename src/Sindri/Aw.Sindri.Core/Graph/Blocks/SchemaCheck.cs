using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// A small JSON-schema check for a toolkit tool's arguments: the 'required' members, the 'type' of each listed property, and 'additionalProperties: false'. A tool's own server checks the rest.
/// </summary>
internal static class SchemaCheck
{
    /// <summary>What is wrong with the arguments.</summary>
    /// <param name="schemaJson">The tool's input schema.</param>
    /// <param name="arguments">The arguments, or null when the node gave none.</param>
    /// <param name="allowReferences">True while the workflow is being validated: a string holding '{{...}}' stands for a value that is not known yet, so its type isn't checked.</param>
    /// <returns>One sentence per problem; empty when the arguments fit (or the schema isn't one this check reads).</returns>
    internal static IReadOnlyList<string> Problems(string schemaJson, JsonObject? arguments, bool allowReferences)
    {
        if (Read(schemaJson) is not { } schema)
        {
            return [];
        }

        var given = arguments ?? [];
        return [.. Missing(schema, given), .. Misfits(schema, given, allowReferences)];
    }

    private static JsonObject? Read(string schemaJson)
    {
        try
        {
            return JsonNode.Parse(schemaJson) as JsonObject;
        }
        catch (JsonException)   // boundary: the manifest was parsed when it was read, so this is a damaged row; the tool's server will judge the call
        {
            return null;
        }
    }

    private static IEnumerable<string> Missing(JsonObject schema, JsonObject given) =>
        schema["required"] is JsonArray required
            ? required.Select(static r => r?.GetValue<string>()).Where(name => name is not null && !given.ContainsKey(name)).Select(static name => $"'{name}' is required")
            : [];

    private static IEnumerable<string> Misfits(JsonObject schema, JsonObject given, bool allowReferences)
    {
        var properties = schema["properties"] as JsonObject;
        var closed = properties is not null && schema["additionalProperties"] is JsonValue extra && extra.GetValueKind() == JsonValueKind.False;
        foreach (var (name, value) in given)
        {
            if (properties?[name] is not JsonObject definition)
            {
                if (closed)
                {
                    yield return $"'{name}' is not an argument of this tool";
                }
            }
            else if (WantedType(definition) is { } wanted && value is not null && !Fits(value, wanted, allowReferences))
            {
                yield return $"'{name}' must be {wanted}";
            }
        }
    }

    private static string? WantedType(JsonObject definition) =>
        definition["type"] is JsonValue type && type.TryGetValue<string>(out var wanted) ? wanted : null;

    private static bool Fits(JsonNode value, string type, bool allowReferences)
    {
        if (allowReferences && value.GetValueKind() == JsonValueKind.String && value.GetValue<string>().Contains("{{", StringComparison.Ordinal))
        {
            return true;
        }

        var kind = value.GetValueKind();
        return type switch
        {
            "string" => kind == JsonValueKind.String,
            "number" => kind == JsonValueKind.Number,
            "integer" => kind == JsonValueKind.Number && IsWhole(value),
            "boolean" => kind is JsonValueKind.True or JsonValueKind.False,
            "object" => kind == JsonValueKind.Object,
            "array" => kind == JsonValueKind.Array,
            "null" => kind == JsonValueKind.Null,
            _ => true,
        };
    }

    /// <summary>Reads a number node as text, so an int, a long, a double and a parsed element all answer alike (JsonValue.TryGetValue&lt;long&gt; is false for a node holding an int).</summary>
    private static bool IsWhole(JsonNode value)
    {
        var text = value.ToJsonString();
        return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            || (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d == Math.Floor(d));
    }
}
