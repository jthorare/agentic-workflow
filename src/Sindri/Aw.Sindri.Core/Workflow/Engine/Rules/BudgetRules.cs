using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>Budgets and token grants (spec §3.7, §5.5, W7): spend against the state's and the recipe's budget, what is left, and '--grant-tokens n'. P3 unit (c).</summary>
/// <remarks>
/// Two rules, by the instance's version marker (<see cref="TokenSpend.Rounds"/>). Per round (every instance that starts now):
/// a state's budget is each round's allowance, counted by (state, loop round), a fresh one starting with the round; a grant
/// applies to the round it answers and also raises the recipe's allowance; the recipe's budget stays the cap over the whole
/// instance. Pooled (an instance from before that, which Temporal replays as it ran): a state's budget is its lifetime total
/// and a grant raises both allowances. '{budget.tokens.left}' is the lower of the state's allowance and the recipe's. A budget
/// park this class decides carries no actor and no withheld trigger: the decision that took the Step's report sets both.
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
        var round = RoundOf(instance, recipe, state);
        if (spent.IsPerRound && state is not null)
        {
            after = SpendInRound(after, recipe, state, round, tokens);
        }

        return new BudgetVerdict(after, Exhausted(after, recipe, state, round) is { } question ? BudgetPark(question) : null);
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
            left = Remaining(budget, StateUsed(instance, recipe, state!), StateGranted(instance, recipe, state!)); // why: a state budget exists only for a state
        }

        if (recipe.Budget is { } overall)
        {
            var total = Remaining(overall.Tokens, instance.Spent.Total, instance.Spent.Granted);
            left = left is { } l ? Math.Min(l, total) : total;
        }

        return left;
    }

    /// <summary>
    /// '--grant-tokens n' (W7): on a budget park only, any n of at least 1 (owner q93, 2026-10-23: no upper bound). The answer applies it to the parked instance and then ends the park, so it
    /// fits a park once.
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

        var perRound = instance.Spent.IsPerRound;
        if (tokens < 1)
        {
            return WorkflowErrors.TriggerNotAllowed.With($"--grant-tokens {Tokens(tokens)} grants nothing (fix: grant at least 1 token)");   // owner q93: no upper bound
        }

        var granted = instance.Spent with { Granted = instance.Spent.Granted + tokens };
        return instance with { Spent = perRound && Parks.RecipeState(instance) is { } state ? GrantRound(instance, recipe, granted, state, tokens) : granted };
    }

    /// <summary>The grant added to the round it answers (the state's current one).</summary>
    private static TokenSpend GrantRound(WorkflowInstance instance, Recipe recipe, TokenSpend granted, string state, int tokens)
    {
        var current = RoundSpendOf(instance.Spent, state, RoundOf(instance, recipe, state));
        return granted with { Rounds = With(instance.Spent.Rounds, state, current with { Granted = current.Granted + tokens }) };
    }

    /// <summary>Tokens the active state's budget has been spent on: this round's under the per-round rule, the lifetime total under the pooled one.</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <param name="state">The state.</param>
    /// <returns>The tokens spent.</returns>
    internal static int StateUsed(WorkflowInstance instance, Recipe recipe, string state) =>
        instance.Spent.IsPerRound
            ? RoundSpendOf(instance.Spent, state, RoundOf(instance, recipe, state)).Used
            : instance.Spent.ByState.GetValueOrDefault(state);

    /// <summary>The tokens granted against the state's allowance: this round's under the per-round rule, every grant under the pooled one.</summary>
    private static int StateGranted(WorkflowInstance instance, Recipe recipe, string state) =>
        instance.Spent.IsPerRound
            ? RoundSpendOf(instance.Spent, state, RoundOf(instance, recipe, state)).Granted
            : instance.Spent.Granted;

    /// <summary>What has run out for the Step about to start: the park's question, or null while both budgets hold.</summary>
    /// <param name="instance">The instance, in the state it has just entered.</param>
    /// <param name="recipe">Its recipe.</param>
    /// <returns>The question, or null.</returns>
    public static string? Exhausted(WorkflowInstance instance, Recipe recipe)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(recipe);
        var state = Parks.RecipeState(instance);
        return Exhausted(instance.Spent, recipe, state, RoundOf(instance, recipe, state));
    }

    /// <summary>
    /// The park at a Step's start whose budget a kept report spent (kind step): its answer starts that Step under the raised allowance, and a start
    /// is not a round of the state's loop, so it resumes with a restart, not a rerun.
    /// </summary>
    /// <param name="question">What ran out.</param>
    /// <param name="actor">Who caused the park.</param>
    /// <returns>The park.</returns>
    public static Suspension StartPark(string question, string actor) => new(
        SuspensionKind.Park, ParkReason.Budget, ParkKind.Step, Escalation: null, question, actor, Mirrors: null,
        new ResumeAction(ResumeKind.Restart, Withheld: null, Gate: null, Effects: []));

    /// <summary>What ran out after a spend, as the park's question; null while both budgets hold.</summary>
    private static string? Exhausted(TokenSpend spent, Recipe recipe, string? state, int round)
    {
        if (state is not null && StateBudget(recipe, state) is { } budget)
        {
            var here = RoundSpendOf(spent, state, round);
            var (used, granted) = spent.IsPerRound ? (here.Used, here.Granted) : (spent.ByState.GetValueOrDefault(state), spent.Granted);
            if (Remaining(budget, used, granted) == 0)
            {
                return $"{state} used {Tokens(used)} of {Tokens(budget + granted)} tokens{(spent.IsPerRound ? $" (round {round.ToString(CultureInfo.InvariantCulture)})" : string.Empty)}";
            }
        }

        return recipe.Budget is { } overall && Remaining(overall.Tokens, spent.Total, spent.Granted) == 0
            ? $"the instance used {Tokens(spent.Total)} of {Tokens(overall.Tokens + spent.Granted)} tokens"
            : null;
    }

    /// <summary>Adds a run to its round (a fresh round when the state moved to a new one) and records the round's overrun when it went past its allowance.</summary>
    private static TokenSpend SpendInRound(TokenSpend after, Recipe recipe, string state, int round, int tokens)
    {
        var current = RoundSpendOf(after, state, round) with { Round = round };
        current = current with { Used = current.Used + tokens };
        var overruns = after.Overruns ?? [];
        if (StateBudget(recipe, state) is { } budget && current.Used > budget + current.Granted)
        {
            overruns = [.. overruns.Where(o => !string.Equals(o.State, state, StringComparison.Ordinal) || o.Round != round), new RoundOverrun(state, round, current.Used, budget + current.Granted)];
        }

        return after with { Rounds = With(after.Rounds, state, current), Overruns = overruns };
    }

    /// <summary>The state's spend in this round: what its latest recorded round holds when that is this round, else nothing yet.</summary>
    private static RoundSpend RoundSpendOf(TokenSpend spent, string state, int round) =>
        spent.Rounds is { } rounds && rounds.TryGetValue(state, out var latest) && latest.Round == round ? latest : new RoundSpend(round, 0, 0);

    private static Dictionary<string, RoundSpend> With(IReadOnlyDictionary<string, RoundSpend>? rounds, string state, RoundSpend value) =>
        new(rounds ?? new Dictionary<string, RoundSpend>(StringComparer.Ordinal), StringComparer.Ordinal) { [state] = value };

    /// <summary>The loop round the state is in: its loop's counter, 1 for a state with no loop (or before the loop counted one).</summary>
    private static int RoundOf(WorkflowInstance instance, Recipe recipe, string? state) =>
        state is not null && recipe.States.TryGetValue(state, out var target) && target.Loop is { } loop && instance.Loops.TryGetValue(loop, out var counted)
            ? counted.Round
            : 1;

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
