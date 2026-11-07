using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>A check's <c language="text">need</c> object (spec §7.3, one key) as one line of text and back: "ci: ui-tests", "changedWith: a, b", "covers", "body: ^x$", "manual".</summary>
internal static partial class ReviewSettingsNeed
{
    /// <summary>The line for a need as the API wrote it.</summary>
    /// <param name="need">The need, or null.</param>
    /// <returns>"key: value", "key" for a true flag, or empty.</returns>
    internal static string Text(JsonElement? need)
    {
        if (need is not { ValueKind: JsonValueKind.Object } body)
        {
            return string.Empty;
        }

        if (body.TryGetProperty("ci", out var ci) && body.TryGetProperty("app", out var app))
        {
            return "ci: " + Value(ci) + " (app: " + Value(app) + ")";
        }

        foreach (var member in body.EnumerateObject())
        {
            return member.Value.ValueKind == JsonValueKind.True ? member.Name : member.Name + ": " + Value(member.Value);
        }

        return string.Empty;
    }

    /// <summary>True for a need the API refuses to take new (SND-REVIEW-003): <c language="text">command</c>. It is shown and sent back unchanged.</summary>
    /// <param name="need">The need, or null.</param>
    /// <returns>True when the need runs a command.</returns>
    internal static bool IsCommand(JsonElement? need) => need is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty("command", out _);

    /// <summary>Reads a line written like <see cref="Text"/>.</summary>
    /// <param name="text">The line.</param>
    /// <returns>The need, or null when it is none of ci, changedWith, covers, body or manual with the value it takes.</returns>
    internal static JsonElement? Parse(string text)
    {
        var at = text.IndexOf(':', StringComparison.Ordinal);
        var key = (at < 0 ? text : text[..at]).Trim();
        var value = at < 0 ? string.Empty : text[(at + 1)..].Trim();
        var app = string.Empty;
        if (string.Equals(key, "ci", StringComparison.Ordinal) && AppSuffix.Match(value) is { Success: true } suffix)
        {
            (value, app) = (suffix.Groups["ci"].Value.Trim(), suffix.Groups["app"].Value.Trim());
            if (app.Length == 0)
            {
                return null;   // "ci: x (app:)" names no app
            }
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            if (!Member(writer, key, value))
            {
                return null;
            }

            if (app.Length > 0)
            {
                writer.WriteString("app", app);
            }

            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }

    [GeneratedRegex(@"^(?<ci>.*?)\s*\(app:\s*(?<app>.*?)\)$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex AppSuffix { get; }

    private static bool Member(Utf8JsonWriter writer, string key, string value)
    {
        if ((string.Equals(key, "covers", StringComparison.Ordinal) || string.Equals(key, "manual", StringComparison.Ordinal)) && (value.Length == 0 || string.Equals(value, "true", StringComparison.Ordinal)))
        {
            writer.WriteBoolean(key, value: true);
            return true;
        }

        if (value.Length == 0)
        {
            return false;
        }

        if (string.Equals(key, "ci", StringComparison.Ordinal) || string.Equals(key, "body", StringComparison.Ordinal))
        {
            writer.WriteString(key, value);
            return true;
        }

        if (!string.Equals(key, "changedWith", StringComparison.Ordinal))
        {
            return false;
        }

        writer.WriteStartArray(key);
        foreach (var glob in SplitGlobs(value))
        {
            writer.WriteStringValue(glob);
        }

        writer.WriteEndArray();
        return true;
    }

    // Commas separate globs, except inside braces: src/{a,b}/** is one glob.
    private static List<string> SplitGlobs(string value)
    {
        List<string> globs = [];
        var (depth, start) = (0, 0);
        for (var i = 0; i < value.Length; i++)
        {
            depth += value[i] switch { '{' => 1, '}' when depth > 0 => -1, _ => 0 };
            if (value[i] == ',' && depth == 0)
            {
                globs.Add(value[start..i]);
                start = i + 1;
            }
        }

        globs.Add(value[start..]);
        return [.. globs.Select(g => g.Trim()).Where(g => g.Length > 0)];
    }

    private static string Value(JsonElement value) =>
        value.ValueKind == JsonValueKind.Array ? string.Join(", ", value.EnumerateArray().Select(e => e.ToString())) : value.ToString();
}
