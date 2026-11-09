using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>How the flow blocks read a config or trigger value that may be text, a number or true/false (a whole reference keeps its type, so a field may arrive as any of them).</summary>
internal static class ConfigValues
{
    /// <summary>The value as text: a string as itself, any other value as its JSON; null for a missing value.</summary>
    internal static string? Text(JsonNode? node) => node switch
    {
        null => null,
        JsonValue v when v.GetValueKind() == JsonValueKind.String => v.GetValue<string>(),
        _ => node.ToJsonString(),
    };

    /// <summary>The value as a whole number; null when it is missing or not one (text such as "7" counts).</summary>
    internal static int? Int(JsonNode? node) => node switch
    {
        JsonValue v when v.TryGetValue<int>(out var number) => number,
        JsonValue v when v.TryGetValue<string>(out var text) && int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => null,
    };

    /// <summary>The value as true or false; null when it is missing or not one (text "true" and "false" count).</summary>
    internal static bool? Bool(JsonNode? node) => node switch
    {
        JsonValue v when v.TryGetValue<bool>(out var flag) => flag,
        JsonValue v when v.TryGetValue<string>(out var text) && bool.TryParse(text, out var parsed) => parsed,
        _ => null,
    };
}
