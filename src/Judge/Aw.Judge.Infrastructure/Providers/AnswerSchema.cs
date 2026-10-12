using System.Text.Json.Nodes;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>The JSON Schema of an answer: the decision, one of the question's outputs, plus its extra properties.</summary>
internal static class AnswerSchema
{
    /// <summary>The decision is required, the extras optional (claude's --json-schema, cursor's prompt).</summary>
    /// <param name="question">The question.</param>
    /// <returns>The schema.</returns>
    public static JsonObject Lax(QuestionRef question)
    {
        var properties = Properties(question);
        foreach (var (name, fragment) in Extras(question))
        {
            properties[name] = fragment.DeepClone();
        }

        return new JsonObject { ["type"] = "object", ["properties"] = properties, ["required"] = new JsonArray("decision") };
    }

    /// <summary>
    /// OpenAI strict mode (codex's --output-schema rejects anything else): every property required and no others, so
    /// each extra is made nullable instead of optional, and a null extra reads as absent.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <returns>The schema.</returns>
    public static JsonObject Strict(QuestionRef question)
    {
        var properties = Properties(question);
        foreach (var (name, fragment) in Extras(question))
        {
            properties[name] = Nullable(fragment);
        }

        var required = new JsonArray([.. properties.Select(p => (JsonNode?)JsonValue.Create(p.Key))]);
        return new JsonObject { ["type"] = "object", ["properties"] = properties, ["required"] = required, ["additionalProperties"] = false };
    }

    private static JsonObject Properties(QuestionRef question) => new()
    {
        ["decision"] = new JsonObject
        {
            ["type"] = "string",
            ["enum"] = new JsonArray([.. question.Outputs.Select(o => (JsonNode?)JsonValue.Create(o))]),
        },
    };

    private static IEnumerable<KeyValuePair<string, JsonObject>> Extras(QuestionRef question) =>
        question.ExtraProperties ?? Enumerable.Empty<KeyValuePair<string, JsonObject>>();

    private static JsonObject Nullable(JsonObject fragment)
    {
        if (fragment["type"] is JsonValue type && type.TryGetValue<string>(out var name))
        {
            var copy = (JsonObject)fragment.DeepClone();
            copy["type"] = new JsonArray(name, "null");
            return copy;
        }

        return new JsonObject { ["anyOf"] = new JsonArray(fragment.DeepClone(), new JsonObject { ["type"] = "null" }) };
    }
}
