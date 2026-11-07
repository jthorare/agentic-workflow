using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Skills;

/// <summary>Reads one skill's block.yaml, already converted to JSON, into blocks (skill flows spec §3.1 and its checks). A block that fails a check is skipped with the reason, never thrown.</summary>
public static partial class SkillManifestParser
{
    /// <summary>Parses and checks the blocks of one manifest.</summary>
    /// <param name="skill">The skill's folder name ('officeHours'), which the seed invokes.</param>
    /// <param name="path">The block.yaml, for the result and the reasons.</param>
    /// <param name="manifestJson">The manifest as JSON.</param>
    /// <param name="skillMarkdown">The skill's SKILL.md, whose aw-result block the outputs' 'from' are checked against; null when there is none (the check is then skipped, like the script does for a folder without one).</param>
    /// <returns>The blocks that loaded and the ones skipped.</returns>
    public static SkillBlocks Parse(string skill, string path, string manifestJson, string? skillMarkdown)
    {
        ArgumentException.ThrowIfNullOrEmpty(skill);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(manifestJson);
        }
        catch (JsonException e)
        {
            return Skipped(skill, path, "it isn't valid YAML: " + e.Message);
        }

        using (document)
        {
            return Blocks(skill, path, document.RootElement, skillMarkdown);
        }
    }

    private static bool IsSchemaOne(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty("schema", out var schema) && schema.ValueKind == JsonValueKind.Number && schema.TryGetInt32(out var version) && version == 1;

    private static SkillBlocks Blocks(string skill, string path, JsonElement root, string? skillMarkdown)
    {
        if (!IsSchemaOne(root))
        {
            return Skipped(skill, path, "the file needs 'schema: 1'");
        }

        if (!root.TryGetProperty("blocks", out var blocks) || blocks.ValueKind != JsonValueKind.Array || blocks.GetArrayLength() == 0)
        {
            return Skipped(skill, path, "the file needs a non-empty 'blocks' list");
        }

        var loaded = new List<LoadedSkillBlock>();
        var skipped = new List<SkippedSkillBlock>();
        var resultKeys = SkillAwResult.Read(skillMarkdown);
        foreach (var block in blocks.EnumerateArray())
        {
            try
            {
                loaded.Add(Loaded(skill, path, block, resultKeys, skillMarkdown is not null));
            }
            catch (ManifestProblemException problem)
            {
                skipped.Add(new SkippedSkillBlock(Text(block, "id") ?? skill, path, problem.Message));
            }
            catch (Exception e) when (e is InvalidOperationException or FormatException)
            {
                skipped.Add(new SkippedSkillBlock(Text(block, "id") ?? skill, path, "a field has the wrong type: " + e.Message));   // why: a backstop for a value read as the wrong kind; one bad block never takes the file's good ones with it
            }
        }

        return new SkillBlocks(loaded, skipped);
    }

    private static LoadedSkillBlock Loaded(string skill, string path, JsonElement block, SkillAwResult.Shape? shape, bool hasSkillMarkdown)
    {
        var manifest = Block(block);
        SkillAwResult.Check(manifest, shape, hasSkillMarkdown);
        var descriptor = SkillDescriptors.Build(manifest);
        return descriptor.IsError ? throw new ManifestProblemException(descriptor.Error.Message) : new LoadedSkillBlock(skill, manifest, descriptor.Value, path, SkillDescriptors.HashOf(manifest));
    }

    private static SkillBlocks Skipped(string skill, string path, string reason) => new([], [new SkippedSkillBlock(skill, path, reason)]);

    private static SkillBlockManifest Block(JsonElement block)
    {
        var name = Text(block, "id") ?? throw new ManifestProblemException("a block needs an 'id'");
        if (!KebabCase.IsMatch(name))
        {
            throw new ManifestProblemException($"the id '{name}' isn't kebab-case");
        }

        var sentence = Text(block, "sentence");
        if (string.IsNullOrWhiteSpace(sentence))
        {
            throw new ManifestProblemException("a block needs a 'sentence'");
        }

        var inputs = Inputs(block);
        var args = Args(block, inputs);
        var outputs = Outputs(block);
        var outcomes = Outcomes(block);
        var duration = Text(block, "maxDuration");
        if (duration is null || !RecipeDuration.TryFrom(duration, out var span))
        {
            throw new ManifestProblemException("'maxDuration' is a whole number and m, h or d ('3h')");
        }

        var pushes = block.TryGetProperty("pushes", out var p) && p.ValueKind == JsonValueKind.True;
        return new SkillBlockManifest(name, sentence, inputs, args, outputs, outcomes, pushes, span.Span);
    }

    private static List<SkillInput> Inputs(JsonElement block)
    {
        var inputs = new List<SkillInput>();
        if (!block.TryGetProperty("inputs", out var map) || map.ValueKind != JsonValueKind.Object)
        {
            return inputs;
        }

        foreach (var property in map.EnumerateObject())
        {
            var type = ValueType(Text(property.Value, "type"), $"input '{property.Name}'");
            var values = Values(property, type);
            inputs.Add(new SkillInput(property.Name, type, Text(property.Value, "label") ?? property.Name, property.Value.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.True, values));
        }

        return inputs;
    }

    private static List<string>? Values(JsonProperty input, SkillValueType type)
    {
        if (!input.Value.TryGetProperty("values", out var list))
        {
            return null;
        }

        var values = list.ValueKind == JsonValueKind.Array ? list.EnumerateArray().Select(static v => v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty).ToList() : [];
        return type != SkillValueType.Text || values.Count == 0 || values.Exists(string.IsNullOrEmpty)
            ? throw new ManifestProblemException($"input '{input.Name}': 'values' is a non-empty list of strings on a text input")
            : values;
    }

    private static List<SkillArg> Args(JsonElement block, List<SkillInput> inputs)
    {
        var declared = inputs.Select(static i => i.Key).ToHashSet(StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);
        var args = new List<SkillArg>();
        if (block.TryGetProperty("args", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            args.AddRange(list.EnumerateArray().Select(item => Arg(item, declared, used)));
        }

        if (inputs.FirstOrDefault(i => i.Required && !used.Contains(i.Key)) is { } unused)
        {
            throw new ManifestProblemException($"the required input '{unused.Key}' doesn't appear in args");
        }

        return args;
    }

    private static SkillArg Arg(JsonElement item, HashSet<string> declared, HashSet<string> used)
    {
        if (item.ValueKind == JsonValueKind.String)
        {
            var text = item.GetString() ?? string.Empty;
            foreach (Match slot in Slot.Matches(text))
            {
                Declare(slot.Groups["name"].Value, declared, used);
            }

            return new SkillArg(text);
        }

        if (item.ValueKind == JsonValueKind.Object && Text(item, "flag") is { } flag && Text(item, "input") is { } input)
        {
            Declare(input, declared, used);
            return new SkillArg(Text: null, flag, input);
        }

        throw new ManifestProblemException("an args item is a string or { flag, input }");
    }

    private static void Declare(string name, HashSet<string> declared, HashSet<string> used)
    {
        if (!declared.Contains(name))
        {
            throw new ManifestProblemException($"args name '{name}', which isn't a declared input");
        }

        _ = used.Add(name);
    }

    private static List<SkillOutput> Outputs(JsonElement block)
    {
        var outputs = new List<SkillOutput>();
        if (block.TryGetProperty("outputs", out var map) && map.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in map.EnumerateObject())
            {
                var from = Text(property.Value, "from");
                outputs.Add(new SkillOutput(property.Name, ValueType(Text(property.Value, "type"), $"output '{property.Name}'"), string.IsNullOrEmpty(from) ? throw new ManifestProblemException($"output '{property.Name}' needs a 'from'") : from));
            }
        }

        return outputs;
    }

    private static SkillOutcomes Outcomes(JsonElement block)
    {
        if (!block.TryGetProperty("outcomes", out var outcomes) || outcomes.ValueKind != JsonValueKind.Object)
        {
            throw new ManifestProblemException("a block needs 'outcomes'");
        }

        var success = Text(outcomes, "success") ?? throw new ManifestProblemException("'outcomes' needs a 'success'");
        var from = Text(outcomes, "from");
        var map = OutcomeMap(outcomes);

        if (from is not null && !map.ContainsValue(success))
        {
            throw new ManifestProblemException($"the success outcome '{success}' isn't one of the mapped outcomes");
        }

        return new SkillOutcomes(from, map, success);
    }

    private static Dictionary<string, string> OutcomeMap(JsonElement outcomes)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (outcomes.TryGetProperty("map", out var mapped) && mapped.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in mapped.EnumerateObject())
            {
                map[property.Name] = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() ?? string.Empty : throw new ManifestProblemException($"outcome '{property.Name}' maps to something other than a name");
            }
        }

        return map;
    }

    private static SkillValueType ValueType(string? type, string what) => type switch
    {
        "text" => SkillValueType.Text,
        "path" => SkillValueType.Path,
        _ => throw new ManifestProblemException($"{what} has a type that is neither 'text' nor 'path'"),
    };

    private static string? Text(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    [GeneratedRegex("^[a-z][a-z0-9]*(-[a-z0-9]+)*\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex KebabCase { get; }

    [GeneratedRegex("\\{(?<name>[A-Za-z][A-Za-z0-9]*)\\}", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Slot { get; }

}
