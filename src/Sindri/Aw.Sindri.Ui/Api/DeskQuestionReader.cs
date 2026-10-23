using System.Text.Json;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Api;

/// <summary>Reads the body of a "fact:escalation" fact into the questions it holds: an array on the design workflow, a single object on the review loop.</summary>
internal static class DeskQuestionReader
{
    /// <summary>The fact's key.</summary>
    internal const string Key = "fact:escalation";

    /// <summary>Reads the fact; an entry without a question is left out, and a body of any other shape reads as no questions.</summary>
    /// <param name="fact">The fact as the API served it.</param>
    /// <returns>The questions, in the order served.</returns>
    internal static IReadOnlyList<DeskQuestion> Of(FactView fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var body = fact.Body;
        List<JsonElement> entries = [];
        if (body.ValueKind == JsonValueKind.Array)
        {
            entries.AddRange(body.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object));
        }
        else if (body.ValueKind == JsonValueKind.Object)
        {
            entries.Add(body);
        }

        var questions = new List<DeskQuestion>();
        foreach (var entry in entries)
        {
            if (Text(entry, "question") is { Length: > 0 } question)
            {
                questions.Add(new DeskQuestion(Text(entry, "id") is { Length: > 0 } id ? id : "q" + (questions.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), Text(entry, "kind"), question, Options(entry), Text(entry, "lens"), Text(entry, "finding")));
            }
        }

        return questions;
    }

    private static string Text(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    // The engine serves options as a non-empty list of words, or none.
    private static string[] Options(JsonElement entry)
    {
        if (!entry.TryGetProperty("options", out var options) || options.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return [.. options.EnumerateArray().Where(o => o.ValueKind == JsonValueKind.String).Select(o => o.GetString() ?? string.Empty).Where(o => o.Length > 0)];
    }
}
