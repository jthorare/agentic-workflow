using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetNeedsYou;

/// <summary>The human's rows that are read at request time and not folded from events: the managed Onyx's notice and the setup rows (the repo step's failure, and the approve-all plan's rows). Every caller gets them, a session too.</summary>
/// <param name="onyx">The managed Onyx's notice (not set up, or serve could not start it); it is a repo-less row.</param>
/// <param name="setup">The setup rows that are computed (the repo step's failure, the 'setup:mcp' need, and the approve-all plan's 'setup:recipes' and 'recipe:' rows).</param>
/// <param name="scrubber">Scrubs the rows' text.</param>
/// <param name="clock">Stamps the rows.</param>
/// <param name="stalled">The agent runs idle past their launch's idle time (issue #1000); none when this build has no launcher.</param>
/// <param name="views">The items view, for the title of a stalled run's item.</param>
internal sealed class NeedsYouLiveRows(IOnyxNotices onyx, SetupNotices setup, ITextScrubber scrubber, TimeProvider clock, IStalledAgentRuns? stalled = null, IViewReader? views = null)
{
    /// <summary>The Onyx row, the setup rows and the stalled agent runs, in that order.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The rows; empty when a source can't be read.</returns>
    public async ValueTask<IReadOnlyList<NeedsYouRow>> Read(CancellationToken cancellationToken) =>
        [.. await OnyxRows(cancellationToken).ConfigureAwait(false), .. await SetupRows(cancellationToken).ConfigureAwait(false), .. await StalledRows(cancellationToken).ConfigureAwait(false)];

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

    // The setup rows that are computed, not folded.
    private async ValueTask<IReadOnlyList<NeedsYouRow>> SetupRows(CancellationToken cancellationToken)
    {
        var repo = await setup.Repo(cancellationToken).ConfigureAwait(false);
        var mcp = await setup.Mcp(cancellationToken).ConfigureAwait(false);
        var recipes = await setup.Recipes(cancellationToken).ConfigureAwait(false);
        return [.. new[] { repo, mcp }.OfType<NeedsYouRow>(), .. recipes];
    }

    // The managed Onyx's row.
    private async ValueTask<IReadOnlyList<NeedsYouRow>> OnyxRows(CancellationToken cancellationToken)
    {
        var notice = await onyx.Current(cancellationToken).ConfigureAwait(false);
        if (notice is not { IsSuccess: true, Value: { } found })
        {
            return [];
        }

        var row = NeedsYouRows.ForOnyx(found, clock.GetUtcNow(), scrubber);
        return row.IsSuccess ? [row.Value] : [];
    }
}
