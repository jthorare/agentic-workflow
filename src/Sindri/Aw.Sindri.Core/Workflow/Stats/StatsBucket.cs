namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>One week of runs.</summary>
/// <param name="WeekStart">The Monday the week starts, 'yyyy-MM-dd' (UTC).</param>
/// <param name="Runs">Runs that started in it.</param>
/// <param name="HumanRate">The share of them that needed a person; null for none.</param>
public sealed record StatsBucket(string WeekStart, int Runs, double? HumanRate);
