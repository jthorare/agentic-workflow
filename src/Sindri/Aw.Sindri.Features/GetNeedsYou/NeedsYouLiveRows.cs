using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetNeedsYou;

/// <summary>The human's rows that are read at request time and not folded from events: the profile drift row and the setup rows (the repo step's failure, and the approve-all plan's rows). Every caller gets them, a session too.</summary>
/// <param name="setup">The setup rows that are computed (the repo step's failure, the 'setup:mcp' need, and the approve-all plan's 'setup:recipes' and 'recipe:' rows).</param>
/// <param name="scrubber">Scrubs the rows' text.</param>
/// <param name="stalled">The agent runs idle past their launch's idle time (issue #1000); none when this build has no launcher.</param>
/// <param name="views">The items view, for the title of a stalled run's item.</param>
/// <param name="ownerStops">The 'desk:' rows of the instances that wait on the owner at a human-verb wait; none when this build has no reader.</param>
internal sealed class NeedsYouLiveRows(SetupNotices setup, ITextScrubber scrubber, IStalledAgentRuns? stalled = null, IViewReader? views = null, NeedsYouOwnerStops? ownerStops = null)
{
    /// <summary>The profile drift row, the setup rows, the stalled agent runs and the owner-stop rows, in that order.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The rows; empty when a source can't be read.</returns>
    public async ValueTask<IReadOnlyList<NeedsYouRow>> Read(CancellationToken cancellationToken) =>
        [.. await DriftRows(cancellationToken).ConfigureAwait(false), .. await SetupRows(cancellationToken).ConfigureAwait(false), .. await StalledRows(cancellationToken).ConfigureAwait(false), .. await OwnerStopRows(cancellationToken).ConfigureAwait(false)];

    // The profile drift row, computed from the live profile against the approved one (SetupNotices reads both).
    private async ValueTask<IReadOnlyList<NeedsYouRow>> DriftRows(CancellationToken cancellationToken) =>
        await setup.Drift(cancellationToken).ConfigureAwait(false) is { } row ? [row] : [];

    // An agent run idle past its launch's idle time that is still not ended (issue #1000): a 'park:' row the run's own park replaces. A run whose item can't be read is left out.
    private async ValueTask<IReadOnlyList<NeedsYouRow>> StalledRows(CancellationToken cancellationToken)
    {
        if (stalled is null || views is null)
        {
            return [];
        }

        var rows = new List<NeedsYouRow>();
        foreach (var run in await stalled.Find(cancellationToken).ConfigureAwait(false))
        {
            var item = await views.Get<ItemRow>(ViewNames.Items, run.Item.Value, cancellationToken).ConfigureAwait(false);
            if (item is { IsSuccess: true, Value: { } found })
            {
                rows.Add(NeedsYouRows.ForStalledAgent(found, run.Instance, run.TurnEndedAt, scrubber));
            }
        }

        return rows;
    }

    // The 'desk:' rows of the instances at a human-verb wait (a folded row of the same id wins, in the service).
    private async ValueTask<IReadOnlyList<NeedsYouRow>> OwnerStopRows(CancellationToken cancellationToken) =>
        ownerStops is null ? [] : await ownerStops.Read(cancellationToken).ConfigureAwait(false);

    // The setup rows that are computed, not folded.
    private async ValueTask<IReadOnlyList<NeedsYouRow>> SetupRows(CancellationToken cancellationToken)
    {
        var repo = await setup.Repo(cancellationToken).ConfigureAwait(false);
        var mcp = await setup.Mcp(cancellationToken).ConfigureAwait(false);
        var recipes = await setup.Recipes(cancellationToken).ConfigureAwait(false);
        return [.. new[] { repo, mcp }.OfType<NeedsYouRow>(), .. recipes];
    }
}
