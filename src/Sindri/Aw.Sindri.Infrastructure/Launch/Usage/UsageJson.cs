using System.Globalization;
using System.Text.Json;
using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Infrastructure.Launch.Usage;

/// <summary>Reads the few fields the usage readers need from a transcript line; a line that is not what they expect is skipped, never an error.</summary>
internal static class UsageJson
{
    /// <summary>Parses <paramref name="line"/> as a JSON object.</summary>
    /// <param name="line">One transcript line.</param>
    /// <returns>The document, or null when the line is not a JSON object.</returns>
    public static JsonDocument? Parse(string line)
    {
        try
        {
            var document = JsonDocument.Parse(line);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                return document;
            }

            document.Dispose();
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The object property <paramref name="name"/> of <paramref name="element"/>, or null.</summary>
    /// <param name="element">An element.</param>
    /// <param name="name">The property.</param>
    /// <returns>The nested object, or null.</returns>
    public static JsonElement? Object(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var found) && found.ValueKind == JsonValueKind.Object ? found : null;

    /// <summary>The string property <paramref name="name"/>, or null.</summary>
    /// <param name="element">An object.</param>
    /// <param name="name">The property.</param>
    /// <returns>The text, or null.</returns>
    public static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var found) && found.ValueKind == JsonValueKind.String ? found.GetString() : null;

    /// <summary>The whole-number property <paramref name="name"/>, or 0 when it is missing or not a number.</summary>
    /// <param name="element">An object.</param>
    /// <param name="name">The property.</param>
    /// <returns>The count.</returns>
    public static long Count(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var found) && found.ValueKind == JsonValueKind.Number && found.TryGetInt64(out var count) ? Math.Max(count, 0) : 0;

    /// <summary>The ISO-8601 'timestamp' of a line.</summary>
    /// <param name="element">The line's object.</param>
    /// <returns>The instant, or null when it has none or it does not read.</returns>
    public static DateTimeOffset? Timestamp(JsonElement element) =>
        Text(element, "timestamp") is { } text && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at) ? at : null;

    /// <summary>Adds up rows per model, in model order.</summary>
    /// <param name="rows">Rows that may name a model more than once.</param>
    /// <returns>One row per model.</returns>
    public static IReadOnlyList<ModelTokens> PerModel(IEnumerable<ModelTokens> rows) =>
        [.. rows.GroupBy(static r => r.Model, StringComparer.Ordinal).OrderBy(static g => g.Key, StringComparer.Ordinal)
            .Select(static g => new ModelTokens(g.Key, g.Sum(static r => r.Input), g.Sum(static r => r.CacheRead), g.Sum(static r => r.CacheWrite), g.Sum(static r => r.Output)))];

    /// <summary>True when the file is a transcript of session <paramref name="id"/>: named for it, or inside a directory named for it (a subagent's).</summary>
    /// <param name="path">The file.</param>
    /// <param name="id">The session id.</param>
    /// <returns>True when it belongs to the session.</returns>
    public static bool OfSession(string path, string id) =>
        string.Equals(Path.GetFileNameWithoutExtension(path), id, StringComparison.Ordinal)
        || path.Contains(Path.DirectorySeparatorChar + id + Path.DirectorySeparatorChar, StringComparison.Ordinal);
}
