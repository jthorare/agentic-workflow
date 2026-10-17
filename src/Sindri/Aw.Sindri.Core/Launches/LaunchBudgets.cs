using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Launches;

/// <summary>The daily-budget rule for an open (screens spec E-28, launcher L-5). Pure.</summary>
public static class LaunchBudgets
{
    /// <summary>Where <paramref name="target"/> stands against today's <paramref name="spend"/>: a spend counted in tokens only has no limit applied (E-28).</summary>
    /// <param name="agents">The approved profile's 'agents:' block, or null.</param>
    /// <param name="target">The agent.</param>
    /// <param name="spend">Today's spend, or null when the agent has no usage reader.</param>
    /// <returns>The state.</returns>
    public static LaunchBudgetState Of(AgentSettings? agents, AgentId target, AgentSpend? spend) =>
        Of(agents, target, spend is { Basis: SpendBasis.TokensOnly } ? 0m : spend?.Usd);

    /// <summary>Where <paramref name="target"/> stands.</summary>
    /// <param name="agents">The approved profile's 'agents:' block, or null.</param>
    /// <param name="target">The agent.</param>
    /// <param name="spentToday">Its spend today in USD, or null when it has no usage reader.</param>
    /// <returns>The state.</returns>
    public static LaunchBudgetState Of(AgentSettings? agents, AgentId target, decimal? spentToday)
    {
        if (agents?.Targets is not { } targets || !targets.TryGetValue(target.Value, out var limits) || limits.DailyBudget is not { } budget)
        {
            return LaunchBudgetState.None;
        }

        if (spentToday is not { } spent)
        {
            return LaunchBudgetState.NoUsageReader;
        }

        var soft = budget * (agents.SoftLimit ?? AgentSettings.DefaultSoftLimit);
        return spent >= budget ? LaunchBudgetState.Hard : spent >= soft ? LaunchBudgetState.Soft : LaunchBudgetState.Ok;
    }

    /// <summary>The refusal a state earns an open or a launch: SND-LAUNCH-010 at the hard limit, and for a budget with no usage reader. Null otherwise.</summary>
    /// <param name="state">Where the agent stands.</param>
    /// <param name="target">The agent.</param>
    /// <returns>The error, or null when the agent may start.</returns>
    public static Error? Refusal(LaunchBudgetState state, AgentId target) => state switch
    {
        LaunchBudgetState.Hard => SindriErrors.LaunchBudgetReached.With($"{target.Value} has reached its daily budget (fix: wait for tomorrow, raise agents.{target.Value}.dailyBudget, or pick another agent)"),
        LaunchBudgetState.NoUsageReader => SindriErrors.LaunchBudgetReached.With($"{target.Value} has a daily budget but no usage reader, so its spend can't be known (no usage reader; fix: remove agents.{target.Value}.dailyBudget or pick another agent)"),
        LaunchBudgetState.None or LaunchBudgetState.Ok or LaunchBudgetState.Soft => null,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "not a budget state"),
    };
}
