using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Messages;

/// <summary>Writes a <see cref="MessageTs"/> as the plain Slack ts string and reads it back through <see cref="MessageTs.TryParse"/> (a JsonException, so a 400, for any other text).</summary>
public sealed class MessageTsJsonConverter : JsonConverter<MessageTs>
{
    /// <inheritdoc/>
    public override MessageTs Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String && MessageTs.TryParse(reader.GetString(), out var ts)
            ? ts
            : throw new JsonException("A Slack ts is seconds, a dot and six digits.");

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, MessageTs value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.Value);
    }
}
