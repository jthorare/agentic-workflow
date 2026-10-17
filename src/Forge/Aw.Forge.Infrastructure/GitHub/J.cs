using System.Text.Json;

namespace Aw.Forge.Infrastructure.GitHub;

/// <summary>Small readers over a JsonElement, so a missing or null field reads as absent instead of throwing.</summary>
internal static class J
{
    internal static JsonElement? At(this JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value
            : null;

    internal static JsonElement? At(this JsonElement element, string first, string second) => element.At(first)?.At(second);

    internal static string Str(this JsonElement element, string name) =>
        element.At(name) is { ValueKind: JsonValueKind.String } v ? v.GetString() ?? string.Empty : string.Empty;

    internal static string Str(this JsonElement element, string first, string second) =>
        element.At(first) is { } inner ? inner.Str(second) : string.Empty;

    internal static bool Flag(this JsonElement element, string name) =>
        element.At(name) is { ValueKind: JsonValueKind.True };

    internal static bool? NullableFlag(this JsonElement element, string name) =>
        element.At(name) is { } v ? v.ValueKind == JsonValueKind.True : null;

    internal static long Long(this JsonElement element, string name) =>
        element.At(name) is { ValueKind: JsonValueKind.Number } v && v.TryGetInt64(out var n) ? n : 0;

    internal static IEnumerable<JsonElement> Items(this JsonElement element) =>
        element.ValueKind == JsonValueKind.Array ? element.EnumerateArray() : [];

    internal static byte[] Object(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            write(writer);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }
}
