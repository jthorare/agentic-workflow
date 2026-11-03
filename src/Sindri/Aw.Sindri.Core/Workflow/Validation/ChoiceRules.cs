using System.Globalization;
using System.Text.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// The decision-tree rules (DT spec §3.2, §8.1): RCP-CHOICE-001 to -004 for the 'choices' and 'facts' of a
/// step.judge.choose state, RCP-CHOICE-005 for its desk twin, and RCP-CHOICE-006 for the order of its transitions.
/// Pure: reads the bound recipe and the catalog. A list that can't be read gives its own finding and no follow-on ones
/// (the resolvers return null, so the wiring rules skip it).
/// </summary>
internal sealed partial class ChoiceRules : IRecipeRules
{
    private const string RouteId = "step.judge.choose";
    private const string TwinId = "step.desk.choose";
    private const string ListFix = "write the choices as a literal list, so approving the recipe fixes its branches";
    private const string EntryFix = "write it as \"<outcome>: <criterion>\"";

    public IEnumerable<RecipeFinding> Check(RecipeDocument document, ValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);
        var recipe = document.Recipe;
        var routes = Routes(recipe, context.Catalog).ToList();
        var findings = routes.SelectMany(r => Choices(r, document.Source))
            .Concat(routes.SelectMany(r => Facts(r, document.Source)))
            .Concat(routes.SelectMany(r => Images(recipe, r, document.Source)))
            .Concat(Twins(recipe, routes, context.Catalog, document.Source))
            .Concat(routes.SelectMany(r => Order(recipe, r, document.Source)));
        return [.. findings.OrderBy(f => f.Line).ThenBy(f => f.Column).ThenBy(f => f.Rule, StringComparer.Ordinal)];
    }

    /// <summary>A state that runs a Step taking its outcomes from its own 'choices' list.</summary>
    private sealed record Route(string State, StepUse Step, BlockDescriptor Descriptor);

    private static IEnumerable<Route> Routes(Recipe recipe, BlockCatalog catalog) =>
        recipe.States
            .Where(s => s.Value.Step is { } step && catalog.Find(step.Use) is { OutcomesFrom: { } from } && !ParameterLists.IsTwin(from))
            .Select(s => new Route(s.Key, s.Value.Step!, catalog.Find(s.Value.Step!.Use)!)); // why: the Where keeps states with a catalogued Step

    private static string Inv(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>What a non-literal list value is called in a message, or null for a literal.</summary>
    private static string? Computed(WithValue value) => value.Fact is not null ? "{ fact }" : value.Profile is not null ? "{ profile }" : null;

    private static IEnumerable<RecipeFinding> Choices(Route route, RecipeSource source)
    {
        var name = route.Descriptor.OutcomesFrom!; // why: Routes keeps descriptors with OutcomesFrom
        if (!route.Step.With.TryGetValue(name, out var value))
        {
            yield break;   // RCP-BLOCK-003 reports the missing parameter
        }

        var path = $"states.{route.State}.step.with.{name}";
        if (Computed(value) is { } computed)
        {
            yield return source.Finding("RCP-CHOICE-002", path, $"{name} of state '{route.State}' is a {computed} value", ListFix);
            yield break;
        }

        if (value.Literal is not { ValueKind: JsonValueKind.Array } list)
        {
            yield return source.Finding("RCP-CHOICE-002", path, $"{name} of state '{route.State}' isn't a list", ListFix);
            yield break;
        }

        var count = list.GetArrayLength();
        if (count is < 2 or > 32)
        {
            yield return source.Finding(
                "RCP-CHOICE-001", path, $"state '{route.State}' gives '{route.Descriptor.Id.Value}' {Inv(count)} choices", "list 2 to 32 choices, each \"<outcome>: <criterion>\"");
        }

        var parsed = ChoiceList.Parse(list);
        foreach (var fault in parsed.Faults)
        {
            yield return Fault(fault, name, route, path, source);
        }

        foreach (var finding in Names(parsed, route, path, source))
        {
            yield return finding;
        }
    }

    /// <summary>RCP-CHOICE-003: an entry that names an outcome the Step adds itself, or one an earlier entry names.</summary>
    private static IEnumerable<RecipeFinding> Names(ChoiceParse parsed, Route route, string path, RecipeSource source)
    {
        var name = route.Descriptor.OutcomesFrom;
        var count = parsed.Entries.Count + parsed.Faults.Count;
        var good = Enumerable.Range(0, count).Except(parsed.Faults.Select(static f => f.Index)).ToList();   // each good entry's index in the list
        for (var k = 0; k < parsed.Entries.Count; k++)
        {
            var outcome = parsed.Entries[k].Outcome;
            var at = $"{path}[{Inv(good[k])}]";
            var earlier = parsed.Entries.Take(k).ToList().FindIndex(e => e.Outcome == outcome);
            if (route.Descriptor.Outcomes.Contains(outcome))
            {
                yield return source.Finding(
                    "RCP-CHOICE-003", at, $"entry {Inv(good[k] + 1)} of {name} in state '{route.State}' names outcome '{outcome.Value}', which '{route.Descriptor.Id.Value}' adds itself",
                    $"rename the choice; route {outcome.Value} with its own transition");
            }
            else if (earlier >= 0)
            {
                yield return source.Finding(
                    "RCP-CHOICE-003", at, $"entries {Inv(good[earlier] + 1)} and {Inv(good[k] + 1)} of {name} in state '{route.State}' both name outcome '{outcome.Value}'",
                    "give each choice its own outcome");
            }
        }
    }

    private static RecipeFinding Fault(ChoiceFault fault, string name, Route route, string path, RecipeSource source)
    {
        var entry = $"entry {Inv(fault.Index + 1)} of {name} in state '{route.State}'";
        var at = $"{path}[{Inv(fault.Index)}]";
        return fault.Kind switch
        {
            ChoiceFaultKind.Mapping => source.Finding("RCP-CHOICE-002", at, $"{entry} is a YAML mapping", "quote it: \"<key>: <value>\""),
            ChoiceFaultKind.NotText => source.Finding("RCP-CHOICE-002", at, $"{entry} is {fault.Detail}, not text", EntryFix),
            ChoiceFaultKind.NoSeparator => source.Finding("RCP-CHOICE-002", at, $"{entry} has no ': ' after its outcome", EntryFix),
            ChoiceFaultKind.BadName => source.Finding(
                "RCP-CHOICE-002", at, $"{entry} names outcome '{fault.Detail}', which isn't [a-z][A-Za-z0-9]{{0,31}}",
                "start with a-z and use only letters and digits, at most 32 characters"),
            ChoiceFaultKind.BadLength => source.Finding("RCP-CHOICE-002", at, $"{entry} has a criterion of {fault.Detail} characters", "write a criterion of 1 to 400 characters"),
            _ => throw new ArgumentOutOfRangeException(nameof(fault), fault.Kind, "Not a choice fault."),
        };
    }

    private static IEnumerable<RecipeFinding> Facts(Route route, RecipeSource source)
    {
        var name = route.Descriptor.InputsFrom;
        if (name is null || !route.Step.With.TryGetValue(name, out var value))
        {
            yield break;
        }

        var path = $"states.{route.State}.step.with.{name}";
        var id = route.Descriptor.Id.Value;
        if (Computed(value) is { } computed)
        {
            yield return source.Finding("RCP-CHOICE-004", path, $"{name} of state '{route.State}' is a {computed} value", $"write {name} as a literal list of fact keys");
            yield break;
        }

        if (value.Literal is not { ValueKind: JsonValueKind.Array } list)
        {
            yield return source.Finding("RCP-CHOICE-004", path, $"{name} of state '{route.State}' isn't a list", $"write {name} as a literal list of fact keys");
            yield break;
        }

        if (list.GetArrayLength() == 0)
        {
            yield return source.Finding("RCP-CHOICE-004", path, $"state '{route.State}' gives '{id}' no facts to read", $"list at least one fact key under {name}");
        }

        foreach (var finding in FactEntries(list, name, route, path, source))
        {
            yield return finding;
        }
    }

    // RCP-CHOICE-007: the 'images' fact must be one the state can read, as its 'facts' are (the facts recorded on every path into it).
    private static IEnumerable<RecipeFinding> Images(Recipe recipe, Route route, RecipeSource source)
    {
        if (!route.Step.With.TryGetValue("images", out var value) || value.Literal is not { ValueKind: JsonValueKind.String } named
            || !FactKey.TryFrom(named.GetString(), out var key))
        {
            yield break;
        }

        if (!FactsReaching.Of(recipe).TryGetValue(route.State, out var readable) || !readable.Contains(key))
        {
            yield return source.Finding(
                "RCP-CHOICE-007", $"states.{route.State}.step.with.images", $"images names fact {key.Value}, which state '{route.State}' can't read",
                "produce the fact on every path into the state, or add it to the recipe's inputs");
        }
    }

    private static IEnumerable<RecipeFinding> FactEntries(JsonElement list, string name, Route route, string path, RecipeSource source)
    {
        var index = 0;
        foreach (var entry in list.EnumerateArray())
        {
            var text = entry.ValueKind == JsonValueKind.String ? entry.GetString() ?? string.Empty : entry.ToString();
            if (entry.ValueKind != JsonValueKind.String || !FactKey.TryFrom(text, out _))
            {
                yield return source.Finding(
                    "RCP-CHOICE-004", $"{path}[{Inv(index)}]", $"entry {Inv(index + 1)} of {name} in state '{route.State}', '{text}', isn't a fact key", FactKeyHint.Fix(text));
            }

            index++;
        }
    }
}
