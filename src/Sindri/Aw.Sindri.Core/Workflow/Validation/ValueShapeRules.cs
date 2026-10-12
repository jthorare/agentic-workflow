using System.Globalization;
using System.Text.Json;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// The one-of shapes typed binding can't see (spec §3.3, §3.5, §3.6), all RCP-SCHEMA-001: a transition leaves from at
/// least one state, a 'when' is exactly one closed form (with 'is' beside 'fact' and 'below' beside 'loop'), and a
/// 'with:' value or child input is exactly one of a literal, '{ fact }' or '{ profile }'.
/// </summary>
internal static class ValueShapeRules
{
    internal static IEnumerable<RecipeFinding> Check(Recipe recipe, RecipeSource source) =>
    [
        .. recipe.Transitions.SelectMany((t, i) => Transition(string.Create(CultureInfo.InvariantCulture, $"transitions[{i}]"), t, source)),
        .. recipe.States.SelectMany(s => State("states." + s.Key, s.Value, source)),
    ];

    private static IEnumerable<RecipeFinding> Transition(string path, RecipeTransition transition, RecipeSource source)
    {
        if (transition.From.Count == 0)
        {
            yield return source.Finding("RCP-SCHEMA-001", path + ".from", "the transition leaves from no state",
                "name the state it leaves from, or a list of them");
        }

        var findings = transition.When is { } when ? When(path + ".when", when, source) : [];
        findings = findings
            .Concat(transition.Gates.SelectMany((g, j) => With(Indexed(path + ".gates", j) + ".with", g.With, source)))
            .Concat(transition.Effects.SelectMany((e, j) => With(Indexed(path + ".effects", j) + ".with", e.With, source)));
        foreach (var finding in findings)
        {
            yield return finding;
        }
    }

    private static IEnumerable<RecipeFinding> State(string path, RecipeState state, RecipeSource source)
    {
        var step = state.Step is { } use
            ? With(path + ".step.with", use.With, source)
                .Concat(use.Inputs.SelectMany((input, k) => With(Indexed(path + ".step.inputs", k) + ".with", input.With, source)))
            : [];
        var child = state.WaitFor?.Child is { } wait
            ? wait.Inputs.Where(static i => Forms(i.Value) != 1).Select(i => Value(path + ".waitFor.child.inputs." + i.Key.Value, source))
            : [];
        return step.Concat(child);
    }

    private static IEnumerable<RecipeFinding> With(string path, IReadOnlyDictionary<string, WithValue> with, RecipeSource source) =>
        with.Where(static w => Forms(w.Value) != 1).Select(w => Value(path + "." + w.Key, source));

    private static RecipeFinding Value(string path, RecipeSource source) =>
        source.Finding("RCP-SCHEMA-001", path, $"'{path}' isn't exactly one of a literal, {{ fact }} or {{ profile }}",
            "write a literal (a string, number, bool or a list of these), { fact: <key> } or { profile: <path> }");

    private static int Forms(WithValue value) =>
        (value.Literal is { ValueKind: not JsonValueKind.Null } ? 1 : 0) + (value.Fact is null ? 0 : 1) + (value.Profile is null ? 0 : 1);

    private static IEnumerable<RecipeFinding> When(string path, WhenClause when, RecipeSource source)
    {
        var forms = WhenForms(when);
        if (forms.Count != 1)
        {
            yield return source.Finding("RCP-SCHEMA-001", path,
                string.Create(CultureInfo.InvariantCulture, $"the condition names {forms.Count} forms ({(forms.Count == 0 ? "none" : string.Join(", ", forms))})"),
                "use exactly one of { outcome }, { fact, is }, { loop, below: cap }, { all }, { any } or { not }");
        }

        foreach (var finding in Pairs(path, when, source).Concat(Nested(path, when, source)))
        {
            yield return finding;
        }
    }

    private static List<string> WhenForms(WhenClause when)
    {
        var forms = new List<string>();
        AddIf(forms, when.Outcome is not null, "outcome");
        AddIf(forms, when.Fact is not null || when.Is is not null, "fact");
        AddIf(forms, when.Loop is not null || when.Below is not null, "loop");
        AddIf(forms, when.All is not null, "all");
        AddIf(forms, when.Any is not null, "any");
        AddIf(forms, when.Not is not null, "not");
        return forms;
    }

    private static void AddIf(List<string> forms, bool present, string form)
    {
        if (present)
        {
            forms.Add(form);
        }
    }

    private static IEnumerable<RecipeFinding> Pairs(string path, WhenClause when, RecipeSource source)
    {
        if ((when.Fact is null) != (when.Is is null))
        {
            yield return source.Finding("RCP-SCHEMA-001", path, "the condition has only one of 'fact' and 'is'", "write { fact: <key>, is: present | absent }");
        }

        if ((when.Loop is null) != (when.Below is null))
        {
            yield return source.Finding("RCP-SCHEMA-001", path, "the condition has only one of 'loop' and 'below'", "write { loop: <name>, below: cap }");
        }

        if (when.All is { Count: 0 } || when.Any is { Count: 0 })
        {
            yield return source.Finding("RCP-SCHEMA-001", path, "the condition lists no conditions under 'all' or 'any'", "list at least one condition");
        }
    }

    private static IEnumerable<RecipeFinding> Nested(string path, WhenClause when, RecipeSource source) =>
    [
        .. (when.All ?? []).SelectMany((c, i) => When(Indexed(path + ".all", i), c, source)),
        .. (when.Any ?? []).SelectMany((c, i) => When(Indexed(path + ".any", i), c, source)),
        .. when.Not is { } not ? When(path + ".not", not, source) : [],
    ];

    private static string Indexed(string path, int index) => string.Create(CultureInfo.InvariantCulture, $"{path}[{index}]");
}
