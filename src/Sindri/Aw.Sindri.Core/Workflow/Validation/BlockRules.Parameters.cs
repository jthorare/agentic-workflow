using System.Globalization;
using System.Text.Json;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>RCP-BLOCK-003 ('with:' arguments fit the descriptor's parameters) and RCP-THRESH-001/002 (W4: a threshold follows the gate's rung).</summary>
internal sealed partial class BlockRules
{
    private const string JudgeProvidersParameter = "providers";
    private const string JudgeParallelParameter = "parallel";
    private const string JudgeParallelKey = "workflow.judge.parallel";

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
            else if (value.Literal is { } literal && !IsChoiceList(descriptor, name) && Mismatch(parameter.Type, literal) is { } given)
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

    /// <summary>
    /// A Judge Step's literal 'providers' narrows the profile's list (DT-J plan Q8): a name outside <see cref="JudgeProviderNames.Known"/> would silently drop
    /// every provider, so it is refused at validate time (RCP-BLOCK-003). A fact value is no list of provider names either.
    /// </summary>
    private static IEnumerable<RecipeFinding> ProviderLists(BlockReference reference, BlockDescriptor descriptor, RecipeSource source)
    {
        if (!reference.With.TryGetValue(JudgeProvidersParameter, out var value) || !descriptor.Parameters.Any(static p => p.Name == JudgeProvidersParameter))
        {
            yield break;
        }

        var path = $"{reference.Path}.with.{JudgeProvidersParameter}";
        var known = string.Join(", ", JudgeProviderNames.Known);
        if (value.Fact is not null)
        {
            yield return source.Finding("RCP-BLOCK-003", path, $"parameter 'providers' of '{descriptor.Id.Value}' takes a list of provider names, not a fact", $"give it a list of {known}, or {{ profile: workflow.judge.providers }}");
        }
        else if (value.Literal is { ValueKind: JsonValueKind.Array } list)
        {
            foreach (var name in list.EnumerateArray().Where(static e => e.ValueKind == JsonValueKind.String).Select(static e => e.GetString() ?? string.Empty).Where(static n => !JudgeProviderNames.Known.Contains(n, StringComparer.Ordinal)))
            {
                yield return source.Finding("RCP-BLOCK-003", path, $"provider '{name}' isn't one of {known}", $"use one of {known}");
            }
        }
    }

    /// <summary>
    /// A judge Step's 'parallel' is a number or { profile: workflow.judge.parallel } (issue 1440): the runtime resolves only that key for it, so another profile key or a fact
    /// would be dropped without a word, and is refused at validate time (RCP-BLOCK-003).
    /// </summary>
    private static IEnumerable<RecipeFinding> CallWidths(BlockReference reference, BlockDescriptor descriptor, RecipeSource source)
    {
        if (!reference.With.TryGetValue(JudgeParallelParameter, out var value) || !descriptor.Parameters.Any(static p => p.Name == JudgeParallelParameter))
        {
            yield break;
        }

        var given = value.Fact is not null ? "a fact" : value.Profile is { } key && !string.Equals(key, JudgeParallelKey, StringComparison.Ordinal) ? key : null;
        if (given is not null)
        {
            yield return source.Finding(
                "RCP-BLOCK-003", $"{reference.Path}.with.{JudgeParallelParameter}",
                $"parameter 'parallel' of '{descriptor.Id.Value}' takes a number or {{ profile: {JudgeParallelKey} }}, not {given}", $"give it a number, or {{ profile: {JudgeParallelKey} }}");
        }
    }

    /// <summary>
    /// A Step's 'bundle' names the pack the runtime copies into its directory (LN-WF5): the plain word 'style' or 'lenses', and the one its Step works from, so a lens Step
    /// can't be handed the style pack. Checked against the descriptor's 'bundle' parameter (RCP-BLOCK-003).
    /// </summary>
    private static IEnumerable<RecipeFinding> Bundles(BlockReference reference, BlockDescriptor descriptor, RecipeSource source)
    {
        const string name = "bundle";
        if (!reference.With.TryGetValue(name, out var value) || !descriptor.Parameters.Any(static p => p.Name == name))
        {
            yield break;
        }

        var path = $"{reference.Path}.with.{name}";
        var id = descriptor.Id.Value;
        if (value.Fact is not null)
        {
            yield return source.Finding("RCP-BLOCK-003", path, $"parameter '{name}' of '{id}' takes the plain word '{PackBundleNames.Style}' or '{PackBundleNames.Lenses}', not a fact", $"give it {PackBundleNames.Style} or {PackBundleNames.Lenses}");
        }
        else if (value.Literal is { ValueKind: JsonValueKind.String } literal && PackBundleNames.Problem(id, literal.GetString() ?? string.Empty) is { } problem)
        {
            yield return source.Finding("RCP-BLOCK-003", path, $"parameter '{name}' of '{id}' {problem}", $"give it {(id.StartsWith("step.lens.", StringComparison.Ordinal) ? PackBundleNames.Lenses : PackBundleNames.Style)}");
        }
    }

    /// <summary>The list a descriptor takes its outcomes or inputs from: RCP-CHOICE-002 and -004 check its entries, so the type check leaves them alone.</summary>
    private static bool IsChoiceList(BlockDescriptor descriptor, string name) =>
        string.Equals(descriptor.OutcomesFrom, name, StringComparison.Ordinal) || string.Equals(descriptor.InputsFrom, name, StringComparison.Ordinal);

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
