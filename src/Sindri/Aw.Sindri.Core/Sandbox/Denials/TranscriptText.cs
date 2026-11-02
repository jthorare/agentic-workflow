using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aw.Sindri.Core.Sandbox.Denials;

/// <summary>Shared, bounded helpers for reading transcript lines (never throws on bad input).</summary>
internal static partial class TranscriptText
{
    internal const int MaxLineChars = 1 << 20;
    internal const int MaxLines = 20_000;
    internal const int MaxPaths = 200;
    internal const int MaxObservations = 500;

    [GeneratedRegex(@"(?:Read-only file system|No such file or directory|Permission denied|Operation not permitted)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ErrorPhrase { get; }

    [GeneratedRegex(@"'(?<q1>/[^'\r\n]+)'|""(?<q2>/[^""\r\n]+)""|(?<![\w./~-])(?<bare>/[^\s:'""`,;()<>|]+)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex AbsolutePath { get; }

    /// <summary>Parses one line as a JSON object, or null.</summary>
    /// <param name="line">The line.</param>
    /// <returns>The document, which the caller disposes, or null.</returns>
    internal static JsonDocument? Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || line.Length > MaxLineChars)
        {
            return null;
        }

        try
        {
            var doc = JsonDocument.Parse(line);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                return doc;
            }

            doc.Dispose();
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A string property, or null.</summary>
    /// <param name="element">The object.</param>
    /// <param name="name">The property.</param>
    /// <returns>The value.</returns>
    internal static string? Str(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>An object property, or null.</summary>
    /// <param name="element">The object.</param>
    /// <param name="name">The property.</param>
    /// <returns>The value.</returns>
    internal static JsonElement? Obj(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Object ? v : null;

    /// <summary>The absolute paths named on the lines of an error text that carry a filesystem error phrase.</summary>
    /// <param name="text">The tool output.</param>
    /// <returns>The paths, in order, not repeated.</returns>
    internal static IEnumerable<string> FailedPaths(string text)
    {
        foreach (var line in text.Split('\n'))
        {
            if (!ErrorPhrase.IsMatch(line))
            {
                continue;
            }

            foreach (Match m in AbsolutePath.Matches(line))
            {
                var value = m.Groups["q1"].Success ? m.Groups["q1"].Value : m.Groups["q2"].Success ? m.Groups["q2"].Value : m.Groups["bare"].Value;
                value = value.TrimEnd('.', ':');
                if (value.Length > 1)
                {
                    yield return value;
                }
            }
        }
    }

    /// <summary>Adds the failed paths of an error text, bounded and without repeats.</summary>
    /// <param name="text">The tool output.</param>
    /// <param name="paths">The list to add to.</param>
    internal static void AddPaths(string text, List<string> paths)
    {
        foreach (var path in FailedPaths(text))
        {
            if (paths.Count < MaxPaths && !paths.Contains(path, StringComparer.Ordinal))
            {
                paths.Add(path);
            }
        }
    }
}
