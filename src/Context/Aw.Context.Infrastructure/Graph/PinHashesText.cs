using System.Text.Json;

namespace Aw.Context.Infrastructure.Graph;

/// <summary>The per-pin hashes of a map version as the JSON object the 'pin_hashes' column holds.</summary>
internal static class PinHashesText
{
    internal static string? Write(IReadOnlyDictionary<string, string>? hashes) =>
        hashes is null ? null : JsonSerializer.Serialize(hashes.OrderBy(static h => h.Key, StringComparer.Ordinal).ToDictionary(static h => h.Key, static h => h.Value, StringComparer.Ordinal), GraphJsonContext.Default.DictionaryStringString);

    internal static IReadOnlyDictionary<string, string>? Read(string? text) =>
        text is null ? null : JsonSerializer.Deserialize(text, GraphJsonContext.Default.DictionaryStringString);
}
