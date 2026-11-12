using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'prompt' block: asks the model, with every trigger and node-output reference fenced as untrusted, and checks the answer against the 'output' JSON schema; a bad answer fails the node with the reason.</summary>
public sealed class PromptBlock(IGraphModelPort model) : IGraphBlock
{
    // why: the schema was only used to check the answer, so the model never knew it should answer in JSON and replied in prose (#3079)
    private const string JsonInstruction = "Answer with only one JSON object that matches this schema, and no other text:";

    private const string RepairLine = "\n\nYour answer was not a JSON object. Answer again with only the JSON object.";

    /// <inheritdoc />
    public string Type => "prompt";

    /// <inheritdoc />
    public string Title => "Prompt";

    /// <inheritdoc />
    public BlockGroup Group => BlockGroup.AI;

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([new("prompt", BlockFieldType.Text, Caption: "Question for the model"), new("output", BlockFieldType.Map, Caption: "Answer shape")]);

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
                var type = TypeOf(schema).Name ?? "string";
                fields[name] = (string.Equals(type, "integer", StringComparison.Ordinal) ? "number" : type) + (required.Contains(name) ? string.Empty : "?");
            }
        }

        return new BlockOutputs(fields);
    }

    /// <inheritdoc />
    public IReadOnlyList<(string Field, string Problem)> CheckConfig(JsonObject config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config["output"] is JsonObject schema && SchemaProblem(schema) is { } problem ? [("output", problem)] : [];
    }

    /// <inheritdoc />
    public async ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        // why: the raw config is used because the interpolated one has lost which text came from the trigger
        if (context.RawConfig["prompt"] is not JsonValue { } promptValue || !promptValue.TryGetValue<string>(out var raw))
        {
            return SindriErrors.GraphBlockFailed.With("prompt needs a 'prompt' text");
        }

        var prompt = FencedPrompt.Render(raw, context.Resolve, model.Untrusted);

        var schema = context.Config["output"] as JsonObject;
        if (schema is not null && SchemaProblem(schema) is { } unusable)
        {
            return SindriErrors.GraphPromptAnswer.With($"the 'output' schema can't be used: {unusable}");
        }

        if (schema is null)
        {
            var plain = await model.Ask(prompt, cancellationToken).ConfigureAwait(false);
            // why: without an 'output' schema the node has no outputs, so nothing reads the answer and any text is fine
            return plain.IsError ? plain.Error : new BlockResult(new Dictionary<string, JsonNode?>(StringComparer.Ordinal));
        }

        var read = await AskForObject(context, prompt, schema, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var fields = read.Value;
        if (Problem(schema, fields) is { } problem)
        {
            return SindriErrors.GraphPromptAnswer.With(problem);
        }

        return new BlockResult(Outputs(schema, fields));
    }

    private async ValueTask<Result<JsonObject>> AskForObject(BlockContext context, string prompt, JsonObject schema, CancellationToken cancellationToken)
    {
        var instructed = WithInstruction(prompt, schema);
        var asked = await model.Ask(instructed, cancellationToken).ConfigureAwait(false);
        if (asked.IsError)
        {
            return asked.Error;
        }

        var read = Parse(asked.Value);
        if (read.IsSuccess)
        {
            return read;
        }

        // why: one repair ask is cheap and most prose answers turn into JSON when told; a second miss is the model's, so it fails in words
        var again = await model.Ask(instructed + RepairLine, cancellationToken).ConfigureAwait(false);
        if (again.IsError)
        {
            return again.Error;
        }

        read = Parse(again.Value);
        if (read.IsSuccess)
        {
            return read;
        }

        // why: the run page shows the failed step's output, so the raw answer stays there for the owner to read; the engine cuts it to the stored-output cap
        context.LeaveFailureOutput(new JsonObject { ["answer"] = again.Value });
        var shown = again.Value.Trim();
        return SindriErrors.GraphPromptAnswer.With($"The model answered with text, not the JSON this step asks for: \"{(shown.Length > 200 ? shown[..200] : shown)}\"");
    }

    private static string WithInstruction(string prompt, JsonObject schema) => $"{prompt}\n\n{JsonInstruction}\n{schema.ToJsonString()}";

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

    // why: an entry that is not text can't name a field, so it is left out here; SchemaProblem reports it
    private static HashSet<string> Required(JsonNode? schema) =>
        schema?["required"] is JsonArray list ? [.. list.Select(Text).OfType<string>()] : [];

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <summary>A property's type: a name, or the first non-null member of a list of names. No 'type' is no name and no problem.</summary>
    private static (string? Name, string? Problem) TypeOf(JsonNode? spec)
    {
        var type = spec?["type"];
        return type switch
        {
            null => (null, null),
            JsonArray list => list.Select(Text).OfType<string>().FirstOrDefault(static t => !string.Equals(t, "null", StringComparison.Ordinal)) is { } first
                ? (first, null)
                : (null, "'type' lists no type name other than \"null\"; write a name such as \"string\""),
            _ => Text(type) is { } name ? (name, null) : (null, "'type' must be a type name such as \"string\" or a list of names"),
        };
    }

    /// <summary>What makes the schema unusable: a 'required' entry or a property 'type' that is not text.</summary>
    private static string? SchemaProblem(JsonObject schema)
    {
        if (schema["required"] is JsonArray list && list.Any(static n => Text(n) is null))
        {
            return "'required' must list field names as text";
        }

        if (schema["properties"] is JsonObject properties)
        {
            foreach (var (name, spec) in properties)
            {
                if (TypeOf(spec).Problem is { } problem)
                {
                    return $"property '{name}': {problem}";
                }
            }
        }

        return null;
    }

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
                if (answer[name] is { } value && TypeOf(spec).Name is { } type && !Fits(value, type))
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
