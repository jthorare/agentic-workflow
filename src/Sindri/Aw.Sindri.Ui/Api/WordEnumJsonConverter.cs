using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api;

/// <summary>
/// Reads and writes an enum as its wire word. A word this build doesn't know (or a value that is not a string) reads as
/// the enum's fallback, so a word the API adds later still reads and shows, quietly, instead of failing the whole answer.
/// </summary>
/// <typeparam name="T">The enum.</typeparam>
/// <param name="names">Each member and its wire word.</param>
/// <param name="fallback">What an unknown word reads as; also what it writes as.</param>
internal abstract class WordEnumJsonConverter<T>(IReadOnlyList<(T Value, string Name)> names, T fallback) : JsonConverter<T>
    where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? name = null;
        if (reader.TokenType == JsonTokenType.String)
        {
            name = reader.GetString();
        }
        else if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
        {
            reader.Skip();
        }

        foreach (var (value, text) in names)
        {
            if (string.Equals(text, name, StringComparison.Ordinal))
            {
                return value;
            }
        }

        return fallback;
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        foreach (var (candidate, text) in names)
        {
            if (EqualityComparer<T>.Default.Equals(candidate, value))
            {
                writer.WriteStringValue(text);
                return;
            }
        }

        foreach (var (candidate, text) in names)
        {
            if (EqualityComparer<T>.Default.Equals(candidate, fallback))
            {
                writer.WriteStringValue(text);
                return;
            }
        }
    }
}
