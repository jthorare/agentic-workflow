using System.Text.Json;

namespace Aw.Sindri.Infrastructure.Memory;

/// <summary>A fact's tags as a JSON array of strings, written and read without reflection (the store is AOT-safe).</summary>
internal static class TagJson
{
    internal static string Write(IReadOnlyList<string> tags)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (var tag in tags)
            {
                writer.WriteStringValue(tag);
            }

            writer.WriteEndArray();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    internal static string[] Read(string json)
    {
        using var document = JsonDocument.Parse(json);
        return [.. document.RootElement.EnumerateArray().Select(static t => t.GetString() ?? string.Empty)];
    }
}
