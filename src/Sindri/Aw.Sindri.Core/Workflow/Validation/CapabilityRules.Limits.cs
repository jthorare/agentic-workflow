using System.Globalization;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>RCP-CAP-006: loops matched by state, and budgets, at or below the base's. Timeouts aren't compared (§13).</summary>
internal sealed partial class CapabilityRules
{
    private static List<RecipeFinding> Limits(Comparison c)
    {
        var findings = new List<RecipeFinding>();
        foreach (var (name, baseState) in c.Base.States)
        {
            if (!c.Generated.States.TryGetValue(name, out var state))
            {
                continue;
            }

            findings.AddRange(Loop(c, name, state, baseState));
            findings.AddRange(Budget(c, $"state '{name}'", $"states.{name}.budget", state.Budget, baseState.Budget));
        }

        findings.AddRange(Budget(c, "recipe", "budget", c.Generated.Budget, c.Base.Budget));
        return findings;
    }

    /// <summary>The loop on <paramref name="name"/> has a cap at or below the base's loop on the same state. An undeclared loop is RCP-LOOP-001's.</summary>
    private static IEnumerable<RecipeFinding> Loop(Comparison c, string name, RecipeState state, RecipeState baseState)
    {
        if (baseState.Loop is not { } baseName || !c.Base.Loops.TryGetValue(baseName, out var baseLoop))
        {
            yield break;
        }

        var cap = baseLoop.Cap.ToString(CultureInfo.InvariantCulture);
        if (state.Loop is not { } loopName)
        {
            yield return c.Source.Finding(
                "RCP-CAP-006",
                $"states.{name}",
                $"state '{name}' has no loop, but base {c.Base.Id.Value} caps it at {cap} rounds",
                $"put it in a loop with cap {cap} or less");
        }
        else if (c.Generated.Loops.TryGetValue(loopName, out var loop) && loop.Cap > baseLoop.Cap)
        {
            yield return c.Source.Finding(
                "RCP-CAP-006",
                $"loops.{loopName}.cap",
                $"loop '{loopName}' on state '{name}' has cap {loop.Cap.ToString(CultureInfo.InvariantCulture)}, above base {c.Base.Id.Value}'s {cap}",
                $"lower cap to {cap} or less");
        }
    }

    /// <summary>A budget the base sets is present and at or below it; one the base doesn't set may be anything.</summary>
    private static IEnumerable<RecipeFinding> Budget(Comparison c, string subject, string path, BudgetSpec? budget, BudgetSpec? baseBudget)
    {
        if (baseBudget is null)
        {
            yield break;
        }

        var limit = baseBudget.Tokens.ToString(CultureInfo.InvariantCulture);
        if (budget is null)
        {
            yield return c.Source.Finding(
                "RCP-CAP-006",
                path,
                $"{subject} budget is unset, but base {c.Base.Id.Value} sets {limit} tokens",
                $"set it to {limit} tokens or less");
        }
        else if (budget.Tokens > baseBudget.Tokens)
        {
            yield return c.Source.Finding(
                "RCP-CAP-006",
                path + ".tokens",
                $"{subject} budget is {budget.Tokens.ToString(CultureInfo.InvariantCulture)} tokens, above base {c.Base.Id.Value}'s {limit}",
                $"lower it to {limit} tokens or less");
        }
    }
}
