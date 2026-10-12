using System.Globalization;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// RCP-NAME-001 (spec §3.4, owner decision 2026-10-11): state, loop and parameter names match [a-z][a-z0-9-]{0,63}, so
/// a name is never ambiguous in a node path ('states.a.b') and reads the same everywhere. Each bad name is reported once,
/// where it is written: a state or loop at its key, a parameter at its key under each 'with' that names it.
/// </summary>
internal static class NameRules
{
    private const string Fix = "rename it: a lower-case letter, then up to 63 lower-case letters, digits or '-'";

    public static IEnumerable<RecipeFinding> Check(Recipe recipe, RecipeSource source) =>
    [
        .. recipe.States.Keys.Where(static n => !IsName(n)).Select(n => Bad(source, "state", n, "states." + n)),
        .. recipe.Loops.Keys.Where(static n => !IsName(n)).Select(n => Bad(source, "loop", n, "loops." + n)),
        .. Parameters(recipe).Where(static p => !IsName(p.Key)).Select(p => Bad(source, "parameter", p.Key, p.With + "." + p.Key)),
    ];

    /// <summary>Whether <paramref name="name"/> is a lower-case letter, then up to 63 lower-case letters, digits or '-'.</summary>
    internal static bool IsName(string name) =>
        name.Length is >= 1 and <= 64 && name[0] is >= 'a' and <= 'z' && name.All(static c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-');

    private static RecipeFinding Bad(RecipeSource source, string what, string name, string path) =>
        source.Finding("RCP-NAME-001", path, $"{what} name '{name}' doesn't match [a-z][a-z0-9-]{{0,63}}", Fix);

    /// <summary>Every 'with' key of every block use, with the path of its 'with'.</summary>
    private static IEnumerable<(string With, string Key)> Parameters(Recipe recipe)
    {
        foreach (var (name, state) in recipe.States)
        {
            if (state.Step is not { } step)
            {
                continue;
            }

            foreach (var key in step.With.Keys)
            {
                yield return ($"states.{name}.step.with", key);
            }

            for (var j = 0; j < step.Inputs.Count; j++)
            {
                foreach (var key in step.Inputs[j].With.Keys)
                {
                    yield return (string.Create(CultureInfo.InvariantCulture, $"states.{name}.step.inputs[{j}].with"), key);
                }
            }
        }

        for (var i = 0; i < recipe.Transitions.Count; i++)
        {
            var transition = recipe.Transitions[i];
            var uses = transition.Gates.Select((g, j) => (Kind: "gates", Index: j, g.With))
                .Concat(transition.Effects.Select((e, j) => (Kind: "effects", Index: j, e.With)));
            foreach (var (kind, index, with) in uses)
            {
                foreach (var key in with.Keys)
                {
                    yield return (string.Create(CultureInfo.InvariantCulture, $"transitions[{i}].{kind}[{index}].with"), key);
                }
            }
        }
    }
}
