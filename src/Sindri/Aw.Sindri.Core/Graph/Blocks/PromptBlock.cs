using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'prompt' block: asks the model, with every trigger and node-output reference fenced as untrusted, and checks the answer against the 'output' JSON schema; a bad answer fails the node with the reason.</summary>
public sealed partial class PromptBlock(IGraphModelPort model) : IGraphBlock
{
    // why: the schema was only used to check the answer, so the model never knew it should answer in JSON and replied in prose (#3079)
    // why: the model sometimes copied the schema itself as its answer (#3588), so the line says to fill it in and an example answer follows the schema
    private const string JsonInstruction = "Answer with only one JSON object that fills in this schema with your real answer, and no other text. The schema is the shape to follow; never repeat the schema itself:";

    private const string ExampleInstruction = "Example of a good answer:";

    // why: a model that can't answer used to fill the shape with defaults and the run went green (#3224); it now has a way to say so
    private const string CouldNotAnswerInstruction = "If you can't answer from what you were given, set couldNotAnswer to true and say why in the couldNotAnswerWhy field; do not guess or fill the other fields with defaults.";

    private const int ShownLength = 200;

    private static readonly HashSet<string> SchemaKeywords = new(["type", "properties", "required"], StringComparer.Ordinal);

    private const string RepairLine = "\n\nYour answer was not a JSON object. Answer again with only the JSON object.";

    private const string EchoRepairLine = "\n\nYour answer repeated the schema instead of filling it in. Answer again with only the JSON object that holds your real answer.";

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
        // why: a prompt that renders to nothing because its references had no value would ask the model an empty question; say which reference was empty instead (SND-GRAPH-113)
        if (string.IsNullOrWhiteSpace(prompt) && FirstEmptyReference(raw, context.Resolve) is { } empty)
        {
            return SindriErrors.GraphPromptFieldEmpty.With($"the prompt came out empty: {empty} had no value in this input");
        }

        var schema = context.Config["output"] as JsonObject;
        if (schema is not null && SchemaProblem(schema) is { } unusable)
        {
            return SindriErrors.GraphPromptAnswer.With($"the 'output' schema can't be used: {unusable}");
        }

        return schema is null ? await AskPlain(context, prompt, cancellationToken).ConfigureAwait(false) : await AskAndCheck(context, prompt, schema, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<BlockResult>> AskPlain(BlockContext context, string prompt, CancellationToken cancellationToken)
    {
        var plain = await AskChecked(context, prompt, cancellationToken).ConfigureAwait(false);
        // why: without an 'output' schema the node has no outputs, so nothing reads the answer and any text is fine; a non-zero exit still fails
        return plain.IsError ? plain.Error : new BlockResult(new Dictionary<string, JsonNode?>(StringComparer.Ordinal));
    }

    private async ValueTask<Result<BlockResult>> AskAndCheck(BlockContext context, string prompt, JsonObject schema, CancellationToken cancellationToken)
    {
        var read = await AskForObject(context, prompt, schema, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var fields = read.Value;
        // why: checked before the shape, because an answer that says it couldn't answer may not fill the required fields
        if (CouldNotAnswer(fields) is { } why)
        {
            return SindriErrors.GraphPromptCouldNotAnswer.With($"{context.Node.Value} couldn't answer: {why}");
        }

        if (Problem(schema, fields) is not { } problem)
        {
            return new BlockResult(Outputs(schema, fields));
        }

        if (context.FailureOutput?["answer"]?.GetValue<string>() is { } answer)
        {
            context.LeaveRaw(answer);
        }

        return SindriErrors.GraphPromptAnswer.With(problem);
    }

    private static string WithInstruction(string prompt, JsonObject schema) =>
        $"{prompt}\n\n{JsonInstruction}\n{WithImplicitFields(schema).ToJsonString()}\n{ExampleInstruction} {ExampleAnswer(schema)}\n{CouldNotAnswerInstruction}";

    // why: the required fields show what to write (all fields when none is required); a non-text placeholder is not valid JSON, so a copied example fails the parse and gets the repair ask
    private static string ExampleAnswer(JsonObject schema)
    {
        if (schema["properties"] is not JsonObject properties)
        {
            return "{}";
        }

        var required = Required(schema);
        var shown = properties
            .Where(p => required.Count == 0 || required.Contains(p.Key))
            .Select(static p => $"{JsonValue.Create(p.Key).ToJsonString()}: {Placeholder(p.Key, TypeOf(p.Value).Name)}");
        return "{" + string.Join(", ", shown) + "}";
    }

    private static string Placeholder(string name, string? type) => type switch
    {
        "boolean" => "<true or false>",
        "number" or "integer" => "<a number>",
        "array" => "[<items>]",
        "object" => "{<fields>}",
        _ => JsonValue.Create($"<your {name}>").ToJsonString(),
    };

    // why: a JSON-schema-shaped object holding none of the schema's own fields is the model copying the schema, not an answer (#3588); 'type', 'properties' and 'required' are left out of the comparison because the copy always has them
    private static bool IsSchemaEcho(JsonObject answer, JsonObject schema) =>
        string.Equals(Text(answer["type"]), "object", StringComparison.Ordinal)
        && answer["properties"] is JsonObject
        && !(schema["properties"] is JsonObject properties
            && properties.Any(p => !SchemaKeywords.Contains(p.Key) && answer.ContainsKey(p.Key)));

    // why: the two fields are optional and implicit, so they are added to the schema the model sees and never to the node's outputs
    private static JsonObject WithImplicitFields(JsonObject schema)
    {
        var shown = (JsonObject)schema.DeepClone();
        if (shown["properties"] is not JsonObject properties)
        {
            properties = [];
            shown["properties"] = properties;
        }

        properties["couldNotAnswer"] = new JsonObject { ["type"] = "boolean" };
        properties["couldNotAnswerWhy"] = new JsonObject { ["type"] = "string" };
        return shown;
    }

    /// <summary>The model's reason when it says it couldn't answer; null when 'couldNotAnswer' is missing or not true.</summary>
    private static string? CouldNotAnswer(JsonObject answer) =>
        answer["couldNotAnswer"] is JsonValue flag && flag.TryGetValue<bool>(out var set) && set
            ? Text(answer["couldNotAnswerWhy"]) is { Length: > 0 } why ? Shown(why) : "no reason given"
            : null;

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
