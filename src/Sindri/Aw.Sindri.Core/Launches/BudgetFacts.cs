namespace Aw.Sindri.Core.Launches;

/// <summary>An agent's daily budget as 'GET /launch/targets' serves it (screens spec E-28).</summary>
/// <param name="DailyUsd">The profile's dailyBudget, or null when none is set.</param>
/// <param name="SpentUsd">Today's priced spend, or null when the agent has no usage reader.</param>
/// <param name="Fraction">Spend over budget, or null when there is no budget or the spend is tokens only.</param>
/// <param name="State">none, ok, soft, hard, or no-usage-reader (a budget with nothing to count it by).</param>
/// <param name="Basis">How the spend was worked out, or null when the agent has no usage reader.</param>
/// <param name="Tokens">Every token used today.</param>
public sealed record BudgetFacts(decimal? DailyUsd, decimal? SpentUsd, decimal? Fraction, LaunchBudgetState State, SpendBasis? Basis, long Tokens);
