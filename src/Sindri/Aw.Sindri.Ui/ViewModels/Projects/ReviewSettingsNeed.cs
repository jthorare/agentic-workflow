using System.Text.Json;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>A check's <c language="text">need</c> object (spec §7.3, one key) as one line of text and back: "ci: ui-tests", "changedWith: a, b", "covers", "body: ^x$", "manual".</summary>
internal static class ReviewSettingsNeed
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

        foreach (var member in body.EnumerateObject())
        {
            return member.Value.ValueKind == JsonValueKind.True ? member.Name : member.Name + ": " + Value(member.Value);
        }

        return string.Empty;
    }

    /// <summary>Reads a line written like <see cref="Text"/>.</summary>
    /// <param name="text">The line.</param>
    /// <returns>The need, or null when it is none of ci, changedWith, covers, body or manual with the value it takes.</returns>
    internal static JsonElement? Parse(string text)
    {
        var at = text.IndexOf(':', StringComparison.Ordinal);
        var key = (at < 0 ? text : text[..at]).Trim();
        var value = at < 0 ? string.Empty : text[(at + 1)..].Trim();
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            if (!Member(writer, key, value))
            {
                return null;
            }

            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }

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
        foreach (var glob in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            writer.WriteStringValue(glob);
        }

        writer.WriteEndArray();
        return true;
    }

    private static string Value(JsonElement value) =>
        value.ValueKind == JsonValueKind.Array ? string.Join(", ", value.EnumerateArray().Select(e => e.ToString())) : value.ToString();
}
