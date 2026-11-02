using System.Text.Json;

namespace Aw.Context.Core.ConceptSynthesis;

/// <summary>A model's name and one-sentence summary for a group.</summary>
/// <param name="Name">The concept's name.</param>
/// <param name="Summary">What it is, in a sentence.</param>
public sealed record ConceptAnswer(string Name, string Summary)
{
    private const int NameLength = 80;
    private const int SummaryLength = 400;

    /// <summary>Reads the answer: a JSON object (a code fence around it is allowed) with a non-empty "name" and "summary" string. Anything else is no answer.</summary>
    /// <param name="text">The model's text (untrusted).</param>
    /// <returns>The answer, or null when it can't be read.</returns>
    public static ConceptAnswer? Read(string? text)
    {
        var body = Unfence((text ?? "").Trim());
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var name = Plain(Text(document.RootElement, "name"), NameLength);
            var summary = Plain(Text(document.RootElement, "summary"), SummaryLength);
            return name.Length == 0 || summary.Length == 0 ? null : new ConceptAnswer(name, summary);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Unfence(string text)
    {
        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        var start = text.IndexOf('\n', StringComparison.Ordinal);
        var end = text.LastIndexOf("```", StringComparison.Ordinal);
        return start < 0 || end <= start ? text : text[(start + 1)..end].Trim();
    }

    private static string? Text(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string Plain(string? text, int limit)
    {
        var flat = string.Join(' ', new string([.. (text ?? "").Select(c => char.IsControl(c) ? ' ' : c)]).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return flat.Length > limit ? flat[..limit].TrimEnd() : flat;
    }
}
