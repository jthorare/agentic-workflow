namespace Aw.Evidence.Core.Judging;

/// <summary>
/// The model-backed judge. <see cref="IsAvailable"/> says whether a judge is wired at all: callers check it first and
/// don't call (or record a model invocation for) a judge that isn't there. A wired judge's methods return null when the
/// call times out, fails or answers out of contract. Either way callers fail closed: no repair (the step stays broken),
/// no critique (the visual stays unchecked).
/// </summary>
public interface IJudge
{
    /// <summary>Whether a judge is wired: false means no model would be called, so there is nothing to record.</summary>
    public bool IsAvailable { get; }

    /// <summary>Whether the judge sends images to Jev, as the judge itself would decide it; false when it can't be read.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when images go to Jev.</returns>
    public ValueTask<bool> JevImagesOnAsync(CancellationToken cancellationToken);

    /// <summary>Proposes a visible element to replace a broken selector.</summary>
    /// <param name="request">The broken selector and candidates.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The proposal, or null when the call failed or answered out of contract.</returns>
    public ValueTask<SelectorRepair?> RepairSelectorAsync(SelectorRepairRequest request, CancellationToken cancellationToken);

    /// <summary>Judges a screenshot.</summary>
    /// <param name="request">The screenshots.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The verdict, or null when the call failed or answered out of contract.</returns>
    public ValueTask<VisualVerdict?> CritiqueAsync(VisualCritiqueRequest request, CancellationToken cancellationToken);
}
