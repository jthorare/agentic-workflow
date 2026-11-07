namespace Aw.Sindri.Core.Profile;

/// <summary>Which settings a repo's PRs get (spec §7.2).</summary>
/// <param name="Settings">The resolved settings; null when <paramref name="Choices"/> has more than one.</param>
/// <param name="Choices">The project entries that name the repo when more than one does (the page asks the owner to pick); empty otherwise.</param>
public sealed record PrReviewPick(PrReviewResolved? Settings, IReadOnlyList<PrReviewProject> Choices)
{
    /// <summary>Whether several project entries name the repo.</summary>
    public bool Ambiguous => Choices.Count > 1;
}
