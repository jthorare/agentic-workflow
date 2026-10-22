namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The stats subset a recipe list row carries (API E-25), counted over the list's window.</summary>
/// <param name="Runs">Runs that started in the window.</param>
/// <param name="Done">Runs that finished.</param>
/// <param name="Failed">Runs that failed.</param>
/// <param name="HumanRate">The share of runs that needed a person (0 to 1); null when there were no runs.</param>
/// <param name="MedianSeconds">The typical run time in seconds; null when no run finished.</param>
/// <param name="TokensP50">The typical tokens a run used; null when none were counted.</param>
/// <param name="Buckets">Four weekly buckets, oldest first.</param>
internal sealed record RecipeListStats(int Runs, int Done, int Failed, double? HumanRate, long? MedianSeconds, long? TokensP50, IReadOnlyList<StatsBucketView> Buckets);
