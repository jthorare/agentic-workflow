using System.Text.Json;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Api;

/// <summary>Reads the body of the scoping interview's "fact:questions" into its questions. The shape of the options decides the form: plain words are the design review's questions, objects are the interview's.</summary>
internal static class ScopingQuestionReader
{
    /// <summary>The fact's key.</summary>
    internal const string Key = "fact:questions";

    /// <summary>True when the body is a list of questions whose options are objects (the interview), not words (the design review).</summary>
    /// <param name="fact">The fact as served.</param>
    /// <returns>True for the interview's shape.</returns>
    internal static bool IsScoping(FactView fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        return fact.Body.ValueKind == JsonValueKind.Array
            && fact.Body.EnumerateArray().Any(q => q.ValueKind == JsonValueKind.Object && q.TryGetProperty("options", out var options) && options.ValueKind == JsonValueKind.Array
                && options.EnumerateArray().Any(o => o.ValueKind == JsonValueKind.Object));
    }

    /// <summary>Reads the questions; an entry without a question or an id is left out.</summary>
    /// <param name="fact">The fact as served.</param>
    /// <returns>The questions, in the order served.</returns>
    internal static IReadOnlyList<ScopingQuestion> Of(FactView fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var questions = new List<ScopingQuestion>();
        if (fact.Body.ValueKind != JsonValueKind.Array)
        {
            return questions;
        }

        foreach (var entry in fact.Body.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object))
        {
            if (Text(entry, "id") is { Length: > 0 } id && Text(entry, "question") is { Length: > 0 } question)
            {
                questions.Add(new ScopingQuestion(id, question, Text(entry, "dimension"), Options(entry), Flag(entry, "other"), Flag(entry, "skip")));
            }
        }

        return questions;
    }

    private static List<ScopingOption> Options(JsonElement entry)
    {
        List<ScopingOption> options = [];
        if (entry.TryGetProperty("options", out var served) && served.ValueKind == JsonValueKind.Array)
        {
            options.AddRange(served.EnumerateArray().Where(o => o.ValueKind == JsonValueKind.Object && Text(o, "id").Length > 0 && Text(o, "answer").Length > 0)
                .Select(o => new ScopingOption(Text(o, "id"), Text(o, "answer"), Text(o, "rationale"), Flag(o, "recommended"))));
        }

        return options;
    }

    private static string Text(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static bool Flag(JsonElement entry, string name) => entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
