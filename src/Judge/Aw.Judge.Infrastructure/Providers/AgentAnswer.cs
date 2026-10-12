using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>
/// Reads a model's answer object against the question: a string decision among the outputs, an optional confidence,
/// and the declared extras (each checked against its fragment's JSON type; a null extra is absent; undeclared fields
/// are dropped).
/// </summary>
internal static class AgentAnswer
{
    /// <summary>
    /// The confidence of an answer that states none. The agent CLIs answer under a schema with no confidence field,
    /// so a well-formed answer is taken as certain; the question's threshold then never stops a CLI's decision.
    /// </summary>
    public const double DefaultConfidence = 1.0;

    /// <summary>Reads an answer.</summary>
    /// <param name="answer">The answer object, as parsed.</param>
    /// <param name="question">The question.</param>
    /// <returns>Decided, or errored with why.</returns>
    public static ProviderResult Read(JsonNode? answer, QuestionRef question)
    {
        if (answer is not JsonObject fields || fields["decision"] is not JsonValue value || !value.TryGetValue<string>(out var decision))
        {
            return new ProviderErrored(ProviderReasons.UnparseableAnswer);
        }

        if (!question.Outputs.Contains(decision, StringComparer.Ordinal))
        {
            return new ProviderErrored(ProviderReasons.OutOfEnum);
        }

        var extra = new JsonObject();
        foreach (var (name, fragment) in question.ExtraProperties ?? new Dictionary<string, JsonObject>(StringComparer.Ordinal))
        {
            if (fields[name] is not { } field)
            {
                continue;
            }

            if (!Matches(fragment, field))
            {
                return new ProviderErrored(ProviderReasons.ExtraOutOfSchema);
            }

            extra[name] = field.DeepClone();
        }

        return new ProviderDecided(decision, Confidence(fields), ProviderReasons.Model) { Extra = extra.Count > 0 ? extra : null };
    }

    /// <summary>
    /// The last JSON object in free text: the whole text when it is JSON, else the widest object ending at the last '}'.
    /// For CLIs with no schema mode (cursor-agent), whose answer has been seen after a prose preamble.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The object, or null when there is none.</returns>
    public static JsonNode? LastObject(string text)
    {
        if (WireJson.TryParse(text, out var whole))
        {
            return whole;
        }

        var end = text.LastIndexOf('}');
        for (var start = end - 1; start >= 0; start--)
        {
            if (text[start] == '{' && WireJson.TryParse(text[start..(end + 1)], out var found))
            {
                return found;
            }
        }

        return null;
    }

    private static double Confidence(JsonObject fields) =>
        fields["confidence"] is JsonValue value && value.TryGetValue<double>(out var confidence) && confidence is >= 0 and <= 1
            ? confidence
            : DefaultConfidence;

    // The fragment's 'type' (a name or a list of names) against the value's JSON kind; array items are checked too.
    private static bool Matches(JsonObject fragment, JsonNode value)
    {
        var types = fragment["type"] switch
        {
            JsonValue one when one.TryGetValue<string>(out var name) => [name],
            JsonArray many => many.Select(t => t?.GetValue<string>()).OfType<string>().ToList(),
            _ => null,
        };
        if (types is not null && !types.Exists(type => IsType(type, value)))
        {
            return false;
        }

        return fragment["items"] is not JsonObject items || value is not JsonArray array
            || array.All(item => item is not null && Matches(items, item));
    }

    private static bool IsType(string type, JsonNode value) => (type, value.GetValueKind()) switch
    {
        ("string", JsonValueKind.String) => true,
        ("number", JsonValueKind.Number) => true,
        ("integer", JsonValueKind.Number) => value.AsValue().TryGetValue<long>(out _),
        ("boolean", JsonValueKind.True or JsonValueKind.False) => true,
        ("array", JsonValueKind.Array) => true,
        ("object", JsonValueKind.Object) => true,
        _ => false,
    };
}
