using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>Budgets and token grants (spec §3.7, §5.5, W7): spend against the state's and the recipe's budget, what is left, and '--grant-tokens n'. P3 unit (c).</summary>
/// <remarks>
/// Grants raise both allowances: '{budget.tokens.left}' is the lower of the state's budget and the recipe's, each plus
/// every token granted, less what was spent against it. A budget park this class decides carries no actor and no
/// withheld trigger: the decision that took the Step's report sets both.
/// </remarks>
internal static class BudgetRules
{
    /// <summary>A run's spend (spec §3.7): exhausting the state's or the recipe's budget parks with budget (kind step, resume rerun).</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <param name="tokens">The tokens the run spent.</param>
    /// <returns>The new spend, and the park when a budget ran out.</returns>
    public static BudgetVerdict Spend(WorkflowInstance instance, Recipe recipe, int tokens)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentOutOfRangeException.ThrowIfNegative(tokens);
        var state = Parks.RecipeState(instance);
        var spent = instance.Spent;
        var byState = new Dictionary<string, int>(spent.ByState, StringComparer.Ordinal);
        if (state is not null)
        {
            byState[state] = byState.GetValueOrDefault(state) + tokens;
        }

        var after = spent with { Total = spent.Total + tokens, ByState = byState };
        return new BudgetVerdict(after, Exhausted(after, recipe, state) is { } question ? BudgetPark(question) : null);
    }

    /// <summary>The tokens left ('{budget.tokens.left}', and what 'gate.budget.available' reads): the lower of the state's and the recipe's, grants included.</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <returns>The tokens left, never below 0; null when no budget applies.</returns>
    public static int? Left(WorkflowInstance instance, Recipe recipe)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(recipe);
        var state = Parks.RecipeState(instance);
        int? left = null;
        if (StateBudget(recipe, state) is { } budget)
        {
            left = Remaining(budget, instance.Spent.ByState.GetValueOrDefault(state!), instance.Spent.Granted); // why: a state budget exists only for a state
        }

        if (recipe.Budget is { } overall)
        {
            var total = Remaining(overall.Tokens, instance.Spent.Total, instance.Spent.Granted);
            left = left is { } l ? Math.Min(l, total) : total;
        }

        return left;
    }

    /// <summary>
    /// '--grant-tokens n' (W7): on a budget park only, at most the original budget (the parked state's, or the
    /// recipe's when the state has none). The answer applies it to the parked instance and then ends the park, so it
    /// fits a park once. With no budget in the recipe (a 'gate.budget.*' refusal against the profile's budget) the
    /// caller checks the bound.
    /// </summary>
    /// <param name="instance">The parked instance.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <param name="tokens">n.</param>
    /// <returns>The instance with the grant added, or SND-WF-003.</returns>
    public static Result<WorkflowInstance> GrantTokens(WorkflowInstance instance, Recipe recipe, int tokens)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(recipe);
        if (instance.Park != ParkReason.Budget)
        {
            var shown = instance.Park is { } reason ? $"a {Parks.Name(reason)} park" : $"an instance in {instance.State}";
            return WorkflowErrors.TriggerNotAllowed.With(
                $"--grant-tokens answers a budget park, not {shown} (fix: use an answer from allowedCommands in status)");
        }

        var original = StateBudget(recipe, instance.Frame.State) ?? recipe.Budget?.Tokens;
        if (tokens < 1 || tokens > original)
        {
            return WorkflowErrors.TriggerNotAllowed.With(original is { } o
                ? $"--grant-tokens {Tokens(tokens)} is outside 1..{Tokens(o)}, the original budget (fix: use --grant-tokens 1 to --grant-tokens {Tokens(o)})"
                : $"--grant-tokens {Tokens(tokens)} grants nothing (fix: grant at least 1 token)");
        }

        return instance with { Spent = instance.Spent with { Granted = instance.Spent.Granted + tokens } };
    }

    /// <summary>What ran out after a spend, as the park's question; null while both budgets hold.</summary>
    private static string? Exhausted(TokenSpend spent, Recipe recipe, string? state)
    {
        if (StateBudget(recipe, state) is { } budget)
        {
            var used = spent.ByState.GetValueOrDefault(state!); // why: a state budget exists only for a state
            if (Remaining(budget, used, spent.Granted) == 0)
            {
                return $"{state} used {Tokens(used)} of {Tokens(budget + spent.Granted)} tokens";
            }
        }

        return recipe.Budget is { } overall && Remaining(overall.Tokens, spent.Total, spent.Granted) == 0
            ? $"the instance used {Tokens(spent.Total)} of {Tokens(overall.Tokens + spent.Granted)} tokens"
            : null;
    }

    private static int? StateBudget(Recipe recipe, string? state) =>
        state is not null && recipe.States.TryGetValue(state, out var target) ? target.Budget?.Tokens : null;

    private static int Remaining(int budget, int spent, int granted) => (int)Math.Max(0L, (long)budget + granted - spent);

    /// <summary>A budget park from a Step report (kind step): its answer re-runs the Step under the raised allowance (§5.8).</summary>
    private static Suspension BudgetPark(string question) => new(
        SuspensionKind.Park, ParkReason.Budget, ParkKind.Step, Escalation: null, question, Actor: string.Empty, Mirrors: null,
        new ResumeAction(ResumeKind.Rerun, Withheld: null, Gate: null, Effects: []));

    /// <summary>A token count as recipes write it: '200k', '1M', or the plain number.</summary>
    private static string Tokens(int count) => count switch
    {
        0 => "0",
        _ when count % 1_000_000 == 0 => (count / 1_000_000).ToString(CultureInfo.InvariantCulture) + "M",
        _ when count % 1_000 == 0 => (count / 1_000).ToString(CultureInfo.InvariantCulture) + "k",
        _ => count.ToString(CultureInfo.InvariantCulture),
    };
}
