using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>Turns a config value into the text a form field shows, and that text back into the JSON value an op sends, by the field's type (text, boolean, number, text list, object).</summary>
internal static class EditorFieldText
{
    /// <summary>The text a field shows for the value a node's config holds.</summary>
    /// <param name="config">The node's config object (or an undefined element).</param>
    /// <param name="field">The block's field.</param>
    /// <returns>The text; empty when the config has no value for it.</returns>
    internal static string Of(JsonElement config, WorkflowBlockField field)
    {
        if (config.ValueKind != JsonValueKind.Object || !config.TryGetProperty(field.Name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return string.Empty;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() ?? string.Empty;
        }

        return value.ValueKind == JsonValueKind.Array ? string.Join('\n', value.EnumerateArray().Select(Item)) : value.GetRawText();
    }

    /// <summary>The value an op sends for text typed into a field.</summary>
    /// <param name="field">The form field (its type and whether it is required).</param>
    /// <param name="text">The typed text.</param>
    /// <returns>The value (null removes the key) and an empty problem, or a line saying why the text can't be sent.</returns>
    internal static (JsonNode? Value, string Problem) Parse(ConfigFieldViewModel field, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return field.IsRequired ? (null, Labels.WfFieldNeeded) : (null, string.Empty);
        }

        return field.Type switch
        {
            "number" => AsNumber(text),
            "boolean" => AsBoolean(text),
            "text list" => AsList(text),
            "object" => AsObject(text),
            _ => (JsonValue.Create(text), string.Empty),
        };
    }

    private static string Item(JsonElement item) => item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : item.GetRawText();

    private static (JsonNode? Value, string Problem) AsNumber(string text)
    {
        var trimmed = text.Trim();
        if (long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole))
        {
            return (JsonValue.Create(whole), string.Empty);
        }

        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)
            ? (JsonValue.Create(number), string.Empty)
            : (null, Labels.WfFieldNotNumber);
    }

    private static (JsonNode? Value, string Problem) AsBoolean(string text) => text.Trim().ToLowerInvariant() switch
    {
        "true" => (JsonValue.Create(value: true), string.Empty),
        "false" => (JsonValue.Create(value: false), string.Empty),
        _ => (null, Labels.WfFieldNotBoolean),
    };

    private static (JsonNode? Value, string Problem) AsList(string text)
    {
        var items = new JsonArray();
        foreach (var line in text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0))
        {
            items.Add((JsonNode)JsonValue.Create(line));
        }

        return (items, string.Empty);
    }

    private static (JsonNode? Value, string Problem) AsObject(string text)
    {
        try
        {
            return JsonNode.Parse(text) is JsonObject parsed ? (parsed, string.Empty) : (null, Labels.WfFieldNotObject);
        }
        catch (JsonException)
        {
            return (null, Labels.WfFieldNotObject);
        }
    }
}
