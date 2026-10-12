using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>
/// Turns a recipe's short forms into the long form (spec §3.1, W3) before typed binding: 'inputs: [fact:item]' is
/// '[{ key: fact:item }]'; 'step: step.produce' is '{ use: step.produce }' and so is each bare Step input; a
/// transition's scalar 'from' is a one-item list; bare gates and effects are '{ use }'; a bare 'with:' value or child
/// input is '{ literal }'; '200k' tokens are 200000; a loop without 'noProgress' gets the default signals. Anything of
/// an unexpected shape is left as written, for the shape check to report.
/// </summary>
internal static class RecipeNormalizer
{
    public static void Normalize(JsonObject recipe, ReaderFindings findings)
    {
        Wrap(recipe["inputs"] as JsonArray, "key");
        Tokens(recipe["budget"] as JsonObject, "budget", findings);
        foreach (var (name, state) in Members(recipe["states"]))
        {
            State($"states.{name}", state as JsonObject, findings);
        }

        if (recipe["transitions"] is JsonArray transitions)
        {
            for (var i = 0; i < transitions.Count; i++)
            {
                Transition(string.Create(CultureInfo.InvariantCulture, $"transitions[{i}]"), transitions[i] as JsonObject, findings);
            }
        }

        foreach (var (_, loop) in Members(recipe["loops"]))
        {
            if (loop is JsonObject spec && !spec.ContainsKey("noProgress"))
            {
                spec["noProgress"] = new JsonArray([.. LoopSpec.DefaultNoProgress.Select(Signal)]);
            }
        }
    }

    private static void State(string path, JsonObject? state, ReaderFindings findings)
    {
        if (state is null)
        {
            return;
        }

        if (state["step"] is JsonValue use)
        {
            state["step"] = new JsonObject { ["use"] = use.DeepClone() };
        }

        if (state["step"] is JsonObject step)
        {
            With(step["with"] as JsonObject, path + ".step.with", findings);
            Wrap(step["inputs"] as JsonArray, "use");
            Uses(step["inputs"] as JsonArray, path + ".step.inputs", findings);
        }

        Tokens(state["budget"] as JsonObject, path + ".budget", findings);
        if (state["waitFor"] is JsonObject { } waitFor && waitFor["child"] is JsonObject child)
        {
            With(child["inputs"] as JsonObject, path + ".waitFor.child.inputs", findings);
        }
    }

    private static void Transition(string path, JsonObject? transition, ReaderFindings findings)
    {
        if (transition is null)
        {
            return;
        }

        if (transition["from"] is JsonValue from)
        {
            transition["from"] = new JsonArray(from.DeepClone());
        }

        foreach (var list in (string[])["gates", "effects"])
        {
            Wrap(transition[list] as JsonArray, "use");
            Uses(transition[list] as JsonArray, $"{path}.{list}", findings);
        }
    }

    /// <summary>Normalizes the 'with:' map of each '{ use, with }' item in <paramref name="items"/>.</summary>
    private static void Uses(JsonArray? items, string path, ReaderFindings findings)
    {
        for (var i = 0; items is not null && i < items.Count; i++)
        {
            if (items[i] is JsonObject item)
            {
                With(item["with"] as JsonObject, string.Create(CultureInfo.InvariantCulture, $"{path}[{i}].with"), findings);
            }
        }
    }

    /// <summary>Each bare value in <paramref name="items"/> becomes '{ <paramref name="field"/>: value }'.</summary>
    private static void Wrap(JsonArray? items, string field)
    {
        for (var i = 0; items is not null && i < items.Count; i++)
        {
            if (items[i] is JsonValue bare)
            {
                items[i] = new JsonObject { [field] = bare.DeepClone() };
            }
        }
    }

    /// <summary>Each bare value of a 'with:' map or child inputs becomes '{ literal: value }'; a mapping is already long.</summary>
    private static void With(JsonObject? values, string path, ReaderFindings findings)
    {
        foreach (var key in values?.Select(static p => p.Key).ToList() ?? [])
        {
            var value = values![key];   // why: the keys come from values, so it isn't null here
            if (value is JsonObject)
            {
                continue;
            }

            if (value is JsonArray list && list.Any(static item => item is JsonObject or JsonArray))
            {
                findings.Add("RCP-SCHEMA-002", $"{path}.{key}", $"the list '{key}' holds more than strings, numbers and bools",
                    "a literal list holds only strings, numbers and bools; use { fact } or { profile } for anything else");
                findings.Refuse($"{path}.{key}");
                continue;
            }

            values[key] = new JsonObject { ["literal"] = value?.DeepClone() };
        }
    }

    private static void Tokens(JsonObject? budget, string path, ReaderFindings findings)
    {
        if (budget?["tokens"] is not JsonValue tokens || tokens.GetValueKind() != JsonValueKind.String)
        {
            return;
        }

        var text = tokens.GetValue<string>();
        if (TokenCounts.TryParse(text, out var count))
        {
            budget["tokens"] = count;
            return;
        }

        findings.Add("RCP-SCHEMA-002", path + ".tokens", $"'{text}' isn't a token count",
            "write a whole number of at least 1, or one with k or M ('1500', '200k', '2M')");
        findings.Refuse(path + ".tokens");
    }

    private static IEnumerable<KeyValuePair<string, JsonNode?>> Members(JsonNode? node) =>
        node is JsonObject map ? [.. map] : [];

    private static string Signal(NoProgressSignal signal) => signal switch
    {
        NoProgressSignal.Findings => "findings",
        NoProgressSignal.EmptyDiff => "emptyDiff",
        NoProgressSignal.SameFailure => "sameFailure",
        _ => "sameFailure",
    };
}
