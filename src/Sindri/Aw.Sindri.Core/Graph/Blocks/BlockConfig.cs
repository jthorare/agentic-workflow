using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>Reads a node's config values the way the data blocks take them: a value, or one reference that resolved to it.</summary>
internal static class BlockConfig
{
    /// <summary>The text of a string value; null when missing, empty or not text.</summary>
    /// <param name="node">The config value.</param>
    /// <returns>The text, or null.</returns>
    public static string? Text(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String && value.GetValue<string>() is { Length: > 0 } text ? text : null;

    /// <summary>A whole number, written as a number or as numeric text.</summary>
    /// <param name="node">The config value.</param>
    /// <returns>The number, or null when missing or not a whole number.</returns>
    public static int? Whole(JsonNode? node)
    {
        if (node is not JsonValue value)
        {
            return null;
        }

        var kind = value.GetValueKind();
        if (kind == JsonValueKind.Number && value.TryGetValue<int>(out var number))
        {
            return number;
        }

        return kind == JsonValueKind.String && int.TryParse(value.GetValue<string>(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    /// <summary>A moment in time written as text.</summary>
    /// <param name="node">The config value.</param>
    /// <returns>The moment, or null when missing or not a date.</returns>
    public static DateTimeOffset? Moment(JsonNode? node) =>
        Text(node) is { } text && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var moment) ? moment : null;
}
