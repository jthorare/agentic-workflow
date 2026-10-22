namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>The part of <see cref="RecipeStats"/> the recipe list shows (API spec §3.8, E-25).</summary>
/// <param name="Runs">Runs that started in the window.</param>
/// <param name="Done">Runs that ended done.</param>
/// <param name="Failed">Runs that ended failed.</param>
/// <param name="HumanRate">The share of runs that needed a person.</param>
/// <param name="MedianSeconds">The median seconds of the ended runs.</param>
/// <param name="TokensP50">The median tokens a run spent.</param>
/// <param name="Buckets">The four latest weeks, oldest first.</param>
public sealed record RecipeListStats(int Runs, int Done, int Failed, double? HumanRate, long? MedianSeconds, long? TokensP50, IReadOnlyList<StatsBucket> Buckets)
{
    /// <summary>The list's subset of <paramref name="stats"/>.</summary>
    /// <param name="stats">A recipe's stats; not a child-only recipe's.</param>
    /// <returns>The subset.</returns>
    public static RecipeListStats Of(RecipeStats stats)
    {
        ArgumentNullException.ThrowIfNull(stats);
        return new RecipeListStats(stats.Runs ?? 0, stats.Done ?? 0, stats.Failed ?? 0, stats.HumanRate, stats.MedianSeconds, stats.Tokens?.P50, stats.Buckets);
    }
}
