using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Features.GetNeedsYou;

/// <summary>The human's rows that are read at request time and not folded from events: the managed Onyx's notice and the setup rows (the repo step's failure, and the approve-all plan's rows). Every caller gets them, a session too.</summary>
/// <param name="onyx">The managed Onyx's notice (not set up, or serve could not start it); it is a repo-less row.</param>
/// <param name="setup">The setup rows that are computed (the repo step's failure, the 'setup:mcp' need, and the approve-all plan's 'setup:recipes' and 'recipe:' rows).</param>
/// <param name="scrubber">Scrubs the rows' text.</param>
/// <param name="clock">Stamps the rows.</param>
internal sealed class NeedsYouLiveRows(IOnyxNotices onyx, SetupNotices setup, ITextScrubber scrubber, TimeProvider clock)
{
    /// <summary>The Onyx row and the setup rows, in that order.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The rows; empty when a source can't be read.</returns>
    public async ValueTask<IReadOnlyList<NeedsYouRow>> Read(CancellationToken cancellationToken) =>
        [.. await OnyxRows(cancellationToken).ConfigureAwait(false), .. await SetupRows(cancellationToken).ConfigureAwait(false)];

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
