using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Launches;

/// <summary>
/// The price table and the budget facts (screens spec E-28). Pure. The table is data an owner edits: a model it lacks is not guessed at, it makes the
/// spend 'tokens-only' and no limit is applied to it.
/// </summary>
public static class Budgets
{
    private const decimal PerMillion = 1_000_000m;

    /// <summary>USD per million tokens. A model is matched by the longest prefix, so an entry for a newer model beats the family's.</summary>
    private static readonly ModelPrice[] _table =
    [
        new("claude-opus-4-1", 15m, 1.50m, 18.75m, 75m),
        new("claude-opus-4-2", 15m, 1.50m, 18.75m, 75m),
        new("claude-opus-4-0", 15m, 1.50m, 18.75m, 75m),
        new("claude-opus-4-2025", 15m, 1.50m, 18.75m, 75m),
        new("claude-opus-4-5", 5m, 0.50m, 6.25m, 25m),
        new("claude-opus-4-6", 5m, 0.50m, 6.25m, 25m),
        new("claude-sonnet-4", 3m, 0.30m, 3.75m, 15m),
        new("claude-3-7-sonnet", 3m, 0.30m, 3.75m, 15m),
        new("claude-3-5-sonnet", 3m, 0.30m, 3.75m, 15m),
        new("claude-haiku-4-5", 1m, 0.10m, 1.25m, 5m),
        new("claude-3-5-haiku", 0.80m, 0.08m, 1m, 4m),
        new("gpt-5-mini", 0.25m, 0.025m, 0.25m, 2m),
        new("gpt-5", 1.25m, 0.125m, 1.25m, 10m),
    ];

    /// <summary>The price of <paramref name="model"/>, matched by the longest id prefix, or null when the table lacks the model.</summary>
    /// <param name="model">The model id a transcript names.</param>
    /// <returns>The price, or null.</returns>
    public static ModelPrice? PriceOf(string model)
    {
        ArgumentNullException.ThrowIfNull(model);
        ModelPrice? best = null;
        foreach (var price in _table)
        {
            if (model.StartsWith(price.Prefix, StringComparison.OrdinalIgnoreCase) && (best is null || price.Prefix.Length > best.Prefix.Length))
            {
                best = price;
            }
        }

        return best;
    }

    /// <summary>Prices the day's usage.</summary>
    /// <param name="usage">The tokens per model.</param>
    /// <returns>The priced spend; tokens only when any model is unpriced.</returns>
    public static AgentSpend Spend(IEnumerable<ModelTokens> usage)
    {
        ArgumentNullException.ThrowIfNull(usage);
        var usd = 0m;
        long tokens = 0;
        var basis = SpendBasis.PriceTable;
        foreach (var used in usage)
        {
            tokens += used.Total;
            if (PriceOf(used.Model) is { } price)
            {
                usd += ((used.Input * price.Input) + (used.CacheRead * price.CacheRead) + (used.CacheWrite * price.CacheWrite) + (used.Output * price.Output)) / PerMillion;
            }
            else
            {
                basis = SpendBasis.TokensOnly;
            }
        }

        return new AgentSpend(usd, tokens, basis);
    }

    /// <summary>The budget facts of <paramref name="target"/>.</summary>
    /// <param name="agents">The approved profile's 'agents:' block, or null.</param>
    /// <param name="target">The agent.</param>
    /// <param name="spend">Today's spend, or null when the agent has no usage reader.</param>
    /// <returns>The facts.</returns>
    public static BudgetFacts For(AgentSettings? agents, AgentId target, AgentSpend? spend)
    {
        var daily = agents?.Targets is { } targets && targets.TryGetValue(target.Value, out var limits) ? limits.DailyBudget : null;
        return Facts(daily, spend, LaunchBudgets.Of(agents, target, spend));
    }

    /// <summary>The facts over all of <paramref name="targets"/>: budgets and spend added, and the state those totals earn.</summary>
    /// <param name="agents">The approved profile's 'agents:' block, or null.</param>
    /// <param name="targets">Every target's facts.</param>
    /// <returns>The total.</returns>
    public static BudgetFacts Total(AgentSettings? agents, IReadOnlyList<BudgetFacts> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        decimal? daily = targets.Any(static t => t.DailyUsd is not null) ? targets.Sum(static t => t.DailyUsd ?? 0m) : null;
        var counted = targets.Where(static t => t.SpentUsd is not null).ToList();
        decimal? spent = counted.Count > 0 ? counted.Sum(static t => t.SpentUsd ?? 0m) : null;
        var basis = BasisOf(counted);
        var priced = basis == SpendBasis.PriceTable;
        var fraction = Ratio(spent, daily, priced);
        return new BudgetFacts(daily, spent, fraction, TotalState(daily, spent, priced, agents), basis, targets.Sum(static t => t.Tokens));
    }

    /// <summary>Tokens only when any counted target is; null when nothing was counted.</summary>
    private static SpendBasis? BasisOf(List<BudgetFacts> counted)
    {
        if (counted.Count == 0)
        {
            return null;
        }

        return counted.Exists(static t => t.Basis == SpendBasis.TokensOnly) ? SpendBasis.TokensOnly : SpendBasis.PriceTable;
    }

    /// <summary>The state the totals earn; with no budget it is none, and with tokens only (or nothing counted) no limit is applied to the total.</summary>
    private static LaunchBudgetState TotalState(decimal? daily, decimal? spent, bool priced, AgentSettings? agents)
    {
        if (daily is not { } limit)
        {
            return LaunchBudgetState.None;
        }

        return priced ? Classify(spent ?? 0m, limit, agents?.SoftLimit ?? AgentSettings.DefaultSoftLimit) : LaunchBudgetState.Ok;
    }

    private static BudgetFacts Facts(decimal? daily, AgentSpend? spend, LaunchBudgetState state)
    {
        var priced = spend is { Basis: SpendBasis.PriceTable };
        var fraction = Ratio(spend?.Usd, daily, priced);
        return new BudgetFacts(daily, spend?.Usd, fraction, state, spend?.Basis, spend?.Tokens ?? 0);
    }

    /// <summary>Spend over budget, or null when there is no budget or the spend is not in dollars.</summary>
    private static decimal? Ratio(decimal? spent, decimal? daily, bool priced) => priced && spent is { } used && daily is > 0m ? used / daily : null;

    private static LaunchBudgetState Classify(decimal spent, decimal budget, decimal softLimit) =>
        spent >= budget ? LaunchBudgetState.Hard : spent >= budget * softLimit ? LaunchBudgetState.Soft : LaunchBudgetState.Ok;
}
