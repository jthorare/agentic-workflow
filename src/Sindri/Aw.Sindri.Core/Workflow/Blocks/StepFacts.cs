using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>Reads and writes the facts of a line Step on the block runtime: the runner hands a fact over as the string element of its stored JSON text.</summary>
public static class StepFacts
{
    /// <summary>The JSON a fact holds; null when the context does not hold the fact or its text is not JSON.</summary>
    /// <param name="context">The Step's context.</param>
    /// <param name="key">The fact's key, with its 'fact:' prefix.</param>
    /// <returns>The value.</returns>
    public static JsonNode? Read(StepContext context, string key)
    {
        if (!context.Facts.TryGetValue(FactKey.From(key), out var element))
        {
            return null;
        }

        var text = element.ValueKind == JsonValueKind.String ? element.GetString() ?? string.Empty : element.GetRawText();
        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return JsonValue.Create(text);   // a bare path or word the recipe passed as plain text
        }
    }

    /// <summary>The string a fact holds (a path, a sha), or null.</summary>
    /// <param name="context">The Step's context.</param>
    /// <param name="key">The fact's key.</param>
    /// <returns>The text.</returns>
    public static string? Text(StepContext context, string key) => Read(context, key) is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <summary>The keys among <paramref name="keys"/> the context does not hold.</summary>
    /// <param name="context">The Step's context.</param>
    /// <param name="keys">The facts the Step needs.</param>
    /// <returns>The missing ones.</returns>
    public static IReadOnlyList<string> Missing(StepContext context, params string[] keys) => [.. keys.Where(k => !context.Facts.ContainsKey(FactKey.From(k)))];

    /// <summary>The 'head' of an object fact, or null.</summary>
    /// <param name="fact">The fact.</param>
    /// <returns>The head sha.</returns>
    public static string? Head(JsonNode? fact) => Field(fact, "head");

    /// <summary>A non-blank string field of an object fact, or null.</summary>
    /// <param name="fact">The fact.</param>
    /// <param name="name">The field's name.</param>
    /// <returns>The text.</returns>
    public static string? Field(JsonNode? fact, string name) =>
        fact is JsonObject obj && obj[name] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;

    /// <summary>A completed result with the given outputs.</summary>
    /// <param name="outcome">The outcome.</param>
    /// <param name="outputs">Fact key to value.</param>
    /// <returns>The result.</returns>
    public static BlockStepResult Done(string outcome, params (string Key, JsonNode Value)[] outputs) => BlockStepResult.Completed(
        OutcomeName.From(outcome),
        outputs.ToDictionary(static o => FactKey.From(o.Key), static o => Element(o.Value)));

    private static JsonElement Element(JsonNode node)
    {
        using var document = JsonDocument.Parse(node.ToJsonString());
        return document.RootElement.Clone();
    }
}
