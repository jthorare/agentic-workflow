using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>What the detail view needs besides the instance view: the parts only the API read service knows, and the time it is as of.</summary>
/// <param name="Title">The item's title.</param>
/// <param name="Parent">The parent instance, when this is a child.</param>
/// <param name="Counts">The timeline, decision and request counts from the ledger.</param>
/// <param name="AsOf">When the read happened; the composer never reads a clock.</param>
/// <param name="StepRuns">The Step runs folded from the instance's ledger events; none when not given.</param>
/// <param name="RecipeDisplayName">The recipe's display name; null serves none.</param>
/// <param name="Session">The agent session of the open run, from its launch; null when none is known.</param>
/// <param name="ReviewTargets">Where each reviewed fact opens, by fact key (<see cref="DeskReviewTargets"/>); null when none were read.</param>
public sealed record InstanceDetailContext(
    string Title,
    ParentRef? Parent,
    ViewCounts Counts,
    DateTimeOffset AsOf,
    IReadOnlyList<StepRun>? StepRuns = null,
    string? RecipeDisplayName = null,
    SessionHandle? Session = null,
    IReadOnlyDictionary<string, DeskReviewTarget>? ReviewTargets = null)
{
    /// <summary>The open run's wait for a free session slot, from the ledger; null when it does not wait.</summary>
    public StatusWaiting? Waiting { get; init; }
}
