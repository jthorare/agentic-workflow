using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>The status word the recipe list shows (API spec §3.8, E-25): the registry's word with what changed.</summary>
/// <param name="Word">'approved', 'changed', 'draft' or 'revoked'.</param>
/// <param name="Detail">For 'changed', what changed; null otherwise.</param>
public sealed record RecipeStatus(string Word, string? Detail)
{
    /// <summary>The status of <paramref name="line"/>.</summary>
    /// <param name="line">A recipe's registry line.</param>
    /// <returns>The word and, for a changed recipe, what changed.</returns>
    public static RecipeStatus Of(RecipeSummary line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return line.Standing switch
        {
            RecipeStanding.Revoked => new RecipeStatus("revoked", Detail: null),
            RecipeStanding.Unapproved => new RecipeStatus("draft", Detail: null),
            RecipeStanding.Approved when line.Stale.Count > 0 => new RecipeStatus("changed", string.Join(", ", line.Stale.Select(Reason))),
            RecipeStanding.Approved => new RecipeStatus("approved", Detail: null),
            _ => throw new ArgumentOutOfRangeException(nameof(line)),
        };
    }

    private static string Reason(StaleReason reason) => reason switch
    {
        StaleReason.PinDrift => "a pinned block changed",
        StaleReason.ChildStale => "a pinned child changed",
        StaleReason.LegacyShape => "the file is in a legacy shape",
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };
}
