using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.GetNeedsYou;

/// <summary>
/// The 'desk:' rows of the instances that wait on the owner at a human-verb wait, read at request time: the folded view has no recipe, so it cannot know a wait
/// state's exit is the owner's click (<see cref="OwnerStopReader"/> finds them; no recipe is named). Any read that fails leaves that one row off, never the list.
/// </summary>
/// <param name="stops">Finds the instances at a human-verb wait.</param>
/// <param name="views">The 'items' view, for each item's title and repo.</param>
/// <param name="contents">Reads the pull request's status fact.</param>
/// <param name="scrubber">Scrubs the rows' text.</param>
/// <param name="clock">The time the instance's own sentence is served at.</param>
internal sealed class NeedsYouOwnerStops(OwnerStopReader stops, IViewReader views, IFactContents contents, ITextScrubber scrubber, TimeProvider clock)
{
    /// <summary>The owner-stop rows.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The rows; empty when the instances can't be listed, and a row whose read fails is left off.</returns>
    public ValueTask<IReadOnlyList<NeedsYouRow>> Read(CancellationToken cancellationToken)
    {
        _ = (stops, views, contents, scrubber, clock, cancellationToken);
        throw new NotSupportedException();
    }
}
