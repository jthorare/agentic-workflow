namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>GET /sindri/workflow/recipes/{id}/stats?window=&amp;hash= (API P12, E-24): one recipe's run figures. The per-state breakdown and the human-time figures are not read yet.</summary>
/// <param name="Recipe">The recipe id.</param>
/// <param name="Hash">The hash the figures are scoped to, as asked; null for every version.</param>
/// <param name="Window">'7d', '30d' or '90d'.</param>
/// <param name="ChildOnly">True when the recipe only ever ran as a child: the run figures are null.</param>
/// <param name="Runs">Runs that started in the window; null for a child-only recipe.</param>
/// <param name="Done">Runs that finished.</param>
/// <param name="Failed">Runs that failed.</param>
/// <param name="Cancelled">Runs that were cancelled.</param>
/// <param name="Outcomes">How the finished runs got there.</param>
/// <param name="HumanRate">The share of runs that needed a person (0 to 1); null when there were no runs.</param>
/// <param name="MedianSeconds">The typical run time in seconds; null when no run finished.</param>
/// <param name="Tokens">The tokens the runs used.</param>
/// <param name="BudgetHits">Runs that hit their budget.</param>
/// <param name="Buckets">Four weekly buckets, oldest first.</param>
/// <param name="AsOf">When the figures were counted.</param>
internal sealed record RecipeStatsView(string Recipe, string? Hash, string Window, bool ChildOnly, int? Runs, int? Done, int? Failed, int? Cancelled, StatsOutcomesView? Outcomes, double? HumanRate,
    long? MedianSeconds, StatsTokensView? Tokens, int? BudgetHits, IReadOnlyList<StatsBucketView>? Buckets, string? AsOf);
