namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>
/// A recipe's stats over the ledger's workflow events for a window (API spec §3.8, E-24). A run counts when it started inside the window;
/// its outcome is read wherever it ended. A child-only recipe has no run figures of its own: they are null and <paramref name="ChildOnly"/> is true.
/// Cost and per-state loop figures are not served yet (no price table; the loop counters are per loop, not per state).
/// </summary>
/// <param name="Recipe">The recipe id.</param>
/// <param name="Hash">The hash the stats are scoped to, as asked; null for every version.</param>
/// <param name="Window">'7d', '30d' or '90d'.</param>
/// <param name="ChildOnly">True for a recipe only ever run as a child.</param>
/// <param name="Runs">Runs that started in the window.</param>
/// <param name="Done">Runs that ended resolved or already satisfied.</param>
/// <param name="Failed">Runs that ended in any other terminal outcome but cancelled.</param>
/// <param name="Cancelled">Runs that ended cancelled.</param>
/// <param name="Outcomes">The done runs by how much human they needed.</param>
/// <param name="HumanRate">The share of runs with a park answered, an escalation decided or a takeover, counted once per run.</param>
/// <param name="MedianSeconds">The median seconds from start to end of the ended runs.</param>
/// <param name="Tokens">The tokens a run spent.</param>
/// <param name="BudgetHits">Parks for budget.</param>
/// <param name="HumanTime">How long a person took to answer.</param>
/// <param name="ByState">The figures per state, and per gate.</param>
/// <param name="Buckets">The four latest weeks, oldest first.</param>
/// <param name="AsOf">When the stats were read.</param>
public sealed record RecipeStats(
    string Recipe,
    string? Hash,
    string Window,
    bool ChildOnly,
    int? Runs,
    int? Done,
    int? Failed,
    int? Cancelled,
    StatsOutcomes? Outcomes,
    double? HumanRate,
    long? MedianSeconds,
    StatsTokens? Tokens,
    int? BudgetHits,
    StatsHumanTime? HumanTime,
    IReadOnlyList<StateStats> ByState,
    IReadOnlyList<StatsBucket> Buckets,
    DateTimeOffset AsOf)
{
    /// <summary>The stats of a recipe that only ever runs as a child: no run figures of its own.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="hash">The hash asked for, or null.</param>
    /// <param name="window">The window.</param>
    /// <param name="asOf">The read's time.</param>
    /// <returns>The stats, with null figures and <see cref="ChildOnly"/> true.</returns>
    public static RecipeStats ForChildOnly(Ids.RecipeId recipe, string? hash, StatsWindow window, DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(window);
        return new RecipeStats(recipe.Value, hash, window.Text, ChildOnly: true, Runs: null, Done: null, Failed: null, Cancelled: null, Outcomes: null, HumanRate: null, MedianSeconds: null,
            Tokens: null, BudgetHits: null, HumanTime: null, ByState: [], Buckets: [], AsOf: asOf);
    }
}
