using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'prompt' block: asks the model and checks the answer against the 'output' JSON schema; a bad answer fails the node with the reason.</summary>
public sealed class PromptBlock(IGraphModelPort model) : IGraphBlock
{
    /// <inheritdoc />
    public string Type => "prompt";

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([new("prompt", BlockFieldType.Text), new("output", BlockFieldType.Map)]);

    /// <inheritdoc />
    public string Example => """{"prompt":"Summarize {{search.facts}}","output":{"type":"object","required":["summary"],"properties":{"summary":{"type":"string"}}}}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        if (config["output"]?["properties"] is JsonObject properties)
        {
            var required = Required(config["output"]);
            foreach (var (name, schema) in properties)
            {
                var type = schema?["type"]?.GetValue<string>() ?? "string";
                fields[name] = (string.Equals(type, "integer", StringComparison.Ordinal) ? "number" : type) + (required.Contains(name) ? string.Empty : "?");
            }
        }

        return new BlockOutputs(fields);
    }

    /// <inheritdoc />
    public async ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Config["prompt"] is not JsonValue { } promptValue || !promptValue.TryGetValue<string>(out var prompt))
        {
            return SindriErrors.GraphBlockFailed.With("prompt needs a 'prompt' text");
        }

        var schema = context.Config["output"] as JsonObject;
        var asked = await model.Ask(prompt, cancellationToken).ConfigureAwait(false);
        if (asked.IsError)
        {
            return asked.Error;
        }

        var read = Parse(asked.Value);
        if (read.IsError)
        {
            return read.Error;
        }

        var fields = read.Value;
        if (schema is not null && Problem(schema, fields) is { } problem)
        {
            return SindriErrors.GraphPromptAnswer.With(problem);
        }

        return new BlockResult(Outputs(schema, fields));
    }

    private static Dictionary<string, JsonNode?> Outputs(JsonObject? schema, JsonObject answer)
    {
        var outputs = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var name in (schema?["properties"] as JsonObject)?.Select(static p => p.Key) ?? [])
        {
            outputs[name] = answer[name]?.DeepClone();
        }

        return outputs;
    }

    private static Result<JsonObject> Parse(string text)
    {
        try
        {
            return JsonNode.Parse(Unfenced(text)) is JsonObject fields ? fields : SindriErrors.GraphPromptAnswer.With("the answer is not a JSON object");
        }
        catch (JsonException error)
        {
            return SindriErrors.GraphPromptAnswer.With($"the answer is not JSON: {error.Message}");
        }
    }

    private static HashSet<string> Required(JsonNode? schema) =>
        schema?["required"] is JsonArray list ? [.. list.Select(static n => n?.GetValue<string>() ?? string.Empty)] : [];

    // why: models often wrap JSON in a ``` fence; the text inside is still the answer
    private static string Unfenced(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var start = trimmed.IndexOf('\n', StringComparison.Ordinal);
        var end = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        return start >= 0 && end > start ? trimmed[(start + 1)..end].Trim() : trimmed;
    }

    private static string? Problem(JsonObject schema, JsonObject answer)
    {
        foreach (var name in Required(schema).Order(StringComparer.Ordinal))
        {
            if (answer[name] is null)
            {
                return $"the answer is missing the required field '{name}'";
            }
        }

        if (schema["properties"] is JsonObject properties)
        {
            foreach (var (name, spec) in properties)
            {
                if (answer[name] is { } value && spec?["type"]?.GetValue<string>() is { } type && !Fits(value, type))
                {
                    return $"the answer's field '{name}' must be {type}";
                }
            }
        }

        return null;
    }

    private static bool Fits(JsonNode value, string type) => type switch
    {
        "string" => value.GetValueKind() == JsonValueKind.String,
        "number" or "integer" => value.GetValueKind() == JsonValueKind.Number,
        "boolean" => value.GetValueKind() is JsonValueKind.True or JsonValueKind.False,
        "object" => value is JsonObject,
        "array" => value is JsonArray,
        _ => true,
    };
}
