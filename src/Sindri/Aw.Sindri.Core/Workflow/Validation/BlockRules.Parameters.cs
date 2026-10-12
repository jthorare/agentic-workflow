using System.Globalization;
using System.Text.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>RCP-BLOCK-003 ('with:' arguments fit the descriptor's parameters) and RCP-THRESH-001/002 (W4: a threshold follows the gate's rung).</summary>
internal sealed partial class BlockRules
{
    private static IEnumerable<RecipeFinding> Parameters(BlockReference reference, BlockDescriptor descriptor, RecipeSource source)
    {
        var id = descriptor.Id.Value;
        var parameters = descriptor.Parameters.ToDictionary(p => p.Name, StringComparer.Ordinal);
        foreach (var (name, value) in reference.With)
        {
            var path = $"{reference.Path}.with.{name}";
            if (!parameters.TryGetValue(name, out var parameter))
            {
                yield return source.Finding(
                    "RCP-BLOCK-003", path, $"block '{id}' has no parameter '{name}'",
                    parameters.Count == 0 ? $"remove it, since '{id}' takes no parameters" : $"remove it, since '{id}' takes {string.Join(", ", parameters.Keys)}");
            }
            else if (value.Literal is { } literal && Mismatch(parameter.Type, literal) is { } given)
            {
                yield return source.Finding(
                    "RCP-BLOCK-003", path, $"parameter '{name}' of '{id}' takes {Expected(parameter.Type)}, not {given}", $"give it {Expected(parameter.Type)}");
            }
        }

        foreach (var missing in descriptor.Parameters.Where(p => p.Required && !reference.With.ContainsKey(p.Name)))
        {
            yield return source.Finding(
                "RCP-BLOCK-003", $"{reference.Path}.with.{missing.Name}", $"block '{id}' needs parameter '{missing.Name}'",
                $"add with: {{ {missing.Name}: <{Placeholder(missing.Type)}> }}");
        }
    }

    /// <summary>What a literal is when it doesn't fit <paramref name="type"/>, or null when it fits. A fact or profile value is read at run time, so only literals are checked.</summary>
    private static string? Mismatch(ParameterType type, JsonElement literal)
    {
        var fits = type switch
        {
            ParameterType.Text => literal.ValueKind == JsonValueKind.String,
            ParameterType.Number => literal.ValueKind == JsonValueKind.Number,
            ParameterType.Flag => literal.ValueKind is JsonValueKind.True or JsonValueKind.False,
            ParameterType.List => literal.ValueKind == JsonValueKind.Array,
            _ => false,
        };
        if (!fits)
        {
            return Given(literal);
        }

        if (type != ParameterType.List)
        {
            return null;
        }

        var items = literal.EnumerateArray().Select(e => e.ValueKind).ToList();
        return items.Exists(k => k is JsonValueKind.Object or JsonValueKind.Array) ? "a list with an object or a list in it"
            : items.Exists(k => k is JsonValueKind.Null or JsonValueKind.Undefined) ? "a list with null in it"
            : null;
    }

    private static string Given(JsonElement literal) => literal.ValueKind switch
    {
        JsonValueKind.String => "text",
        JsonValueKind.Number => "a number",
        JsonValueKind.True or JsonValueKind.False => "a flag",
        JsonValueKind.Array => "a list",
        JsonValueKind.Object => "an object",
        JsonValueKind.Null or JsonValueKind.Undefined => "null",
        _ => "null",
    };

    private static string Expected(ParameterType type) => type switch
    {
        ParameterType.Text => "text",
        ParameterType.Number => "a number",
        ParameterType.Flag => "a flag (true or false)",
        ParameterType.List => "a list of strings, numbers or flags",
        _ => "a value",
    };

    private static string Placeholder(ParameterType type) => type switch
    {
        ParameterType.Text => "text",
        ParameterType.Number => "number",
        ParameterType.Flag => "true or false",
        ParameterType.List => "list",
        _ => "value",
    };

    private static IEnumerable<RecipeFinding> Thresholds(Recipe recipe, BlockCatalog catalog, RecipeSource source) =>
        recipe.Transitions.SelectMany((t, i) => t.Gates.SelectMany((g, j) =>
            catalog.Find(g.Use) is { Kind: BlockKind.Gate, Rung: { } rung }
                ? Threshold(g, rung, $"transitions[{Inv(i)}].gates[{Inv(j)}].threshold", source)
                : []));

    private static IEnumerable<RecipeFinding> Threshold(GateUse gate, Rung rung, string path, RecipeSource source)
    {
        var id = gate.Use.Value;
        if (rung == Rung.Deterministic)
        {
            return gate.Threshold is null
                ? []
                : [source.Finding("RCP-THRESH-002", path, $"gate '{id}' is deterministic and takes no threshold", "remove threshold")];
        }

        if (gate.Threshold is not { } threshold)
        {
            var asks = rung == Rung.JudgeQuestion ? "a judge question" : "a bounded model call";
            return [source.Finding("RCP-THRESH-001", path, $"gate '{id}' is {asks} and has no threshold", "add threshold: a score above 0 and at most 1, for example 0.8")];
        }

        return threshold is > 0 and <= 1
            ? []
            : [source.Finding(
                "RCP-THRESH-002", path, $"threshold {threshold.ToString(CultureInfo.InvariantCulture)} of gate '{id}' is out of range",
                "use a score above 0 and at most 1")];
    }
}
