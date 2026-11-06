using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>Reads and writes <see cref="FactType"/> as its kebab-case name; a name it doesn't know reads as <see cref="FactType.Payload"/> (API spec section 3.8).</summary>
public sealed class FactTypeJsonConverter : JsonConverter<FactType>
{
    private static readonly (FactType Value, string Name)[] _names =
    [
        (FactType.Text, "text"), (FactType.File, "file"), (FactType.Item, "item"), (FactType.Link, "link"),
        (FactType.ScopeMap, "scope-map"), (FactType.Packet, "packet"), (FactType.Evidence, "evidence"),
        (FactType.Decision, "decision"), (FactType.Payload, "payload"), (FactType.Findings, "findings"),
    ];

    /// <summary>The word the wire and the CLI use for <paramref name="type"/> ('scope-map', 'text', ...).</summary>
    /// <param name="type">The fact type.</param>
    /// <returns>Its word.</returns>
    public static string Word(FactType type) => Array.Find(_names, n => n.Value == type).Name ?? "payload";

    /// <inheritdoc />
    public override FactType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // A name this build doesn't know, null or a structured token all read as payload; the reader is left after the value.
        string? name = null;
        if (reader.TokenType == JsonTokenType.String)
        {
            name = reader.GetString();
        }
        else if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
        {
            reader.Skip();
        }

        foreach (var (value, text) in _names)
        {
            if (string.Equals(text, name, StringComparison.Ordinal))
            {
                return value;
            }
        }

        return FactType.Payload;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, FactType value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        foreach (var (candidate, text) in _names)
        {
            if (candidate == value)
            {
                writer.WriteStringValue(text);
                return;
            }
        }

        writer.WriteStringValue("payload");
    }
}
