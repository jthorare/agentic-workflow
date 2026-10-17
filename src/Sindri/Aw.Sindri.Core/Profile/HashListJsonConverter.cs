using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>Reads one string or a list of strings as a list; writes a single entry as one string, so both spellings hash alike.</summary>
public sealed class HashListJsonConverter : JsonConverter<IReadOnlyList<string>>
{
    /// <inheritdoc />
    public override IReadOnlyList<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return [reader.GetString()!];   // why: a string token has text
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("stepArgvHash must be a hash or a list of hashes");
        }

        var list = new List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            list.Add(reader.TokenType == JsonTokenType.String ? reader.GetString()! : throw new JsonException("stepArgvHash entries must be text"));   // why: a string token has text
        }

        return list;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, IReadOnlyList<string> value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        if (value.Count == 1)
        {
            writer.WriteStringValue(value[0]);
            return;
        }

        writer.WriteStartArray();
        foreach (var hash in value)
        {
            writer.WriteStringValue(hash);
        }

        writer.WriteEndArray();
    }
}
