using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Recipes;

namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>One workflow under the workflows step: its plain name and where its approval stands, "approved", "approved v4, v5 available" or "Out of date". It decides nothing the list did not serve.</summary>
internal sealed class SetupRecipeLineViewModel
{
    /// <summary>The line over what the list served.</summary>
    /// <param name="served">The workflow's row, at its highest version.</param>
    internal SetupRecipeLineViewModel(RecipeSummaryView served)
    {
        ArgumentNullException.ThrowIfNull(served);
        Name = RecipeNames.Of(served.Id, served.DisplayName);
        var newer = ShippedIsNewer(served);
        IsOutdated = newer || ChangedSinceApproval(served);
        Status = newer ? Labels.SetupRecipeNewer(served.ApprovedVersion.GetValueOrDefault(), served.Version) : StatusOf(served, IsOutdated);
        AccessibleName = Name + ": " + Status;
    }

    // The shipped version waits while an older one stays approved.
    private static bool ShippedIsNewer(RecipeSummaryView served) =>
        served.ApprovedVersion < served.Version && !string.Equals(served.Standing, "revoked", StringComparison.Ordinal);

    // An approved version that pins drifted from or whose hash differs from the shipped one.
    private static bool ChangedSinceApproval(RecipeSummaryView served) =>
        string.Equals(served.Standing, "approved", StringComparison.Ordinal)
        && (served.Stale is { Count: > 0 } || (served.ApprovedHash is { } hash && !string.Equals(hash, served.Hash, StringComparison.Ordinal)));

    private static string StatusOf(RecipeSummaryView served, bool outdated) => served.Standing switch
    {
        "approved" => outdated ? Labels.SetupRecipeOutOfDate : Labels.SetupRecipeApproved,
        "revoked" => Labels.StandingWord(RecipeStanding.Revoked),
        _ => Labels.StandingWord(RecipeStanding.NotApproved),
    };

    /// <summary>The plain name.</summary>
    public string Name { get; }

    /// <summary>Where its approval stands, in words.</summary>
    public string Status { get; }

    /// <summary>True when it is approved but older than what ships (or changed since).</summary>
    public bool IsOutdated { get; }

    /// <summary>The row's accessible name.</summary>
    public string AccessibleName { get; }
}
