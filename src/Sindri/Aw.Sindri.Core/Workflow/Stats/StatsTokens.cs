namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>Tokens per run; null figures when no run recorded a spend.</summary>
/// <param name="P50">The median.</param>
/// <param name="P90">The 90th percentile.</param>
public sealed record StatsTokens(long? P50, long? P90);
