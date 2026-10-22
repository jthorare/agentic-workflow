using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Features.GetNeedsYou;

/// <summary>The human's rows that are read at request time and not folded from events: the managed Onyx's notice and the setup rows (the repo step's failure, and the approve-all plan's rows). All are the human's to act on, so a session gets none.</summary>
/// <param name="onyx">The managed Onyx's notice (not set up, or serve could not start it); it is a repo-less row, shown to a human caller only.</param>
/// <param name="setup">The setup rows that are computed (the repo step's failure, and the approve-all plan's 'setup:recipes' and 'recipe:' rows).</param>
/// <param name="scrubber">Scrubs the rows' text.</param>
/// <param name="clock">Stamps the rows.</param>
internal sealed class NeedsYouLiveRows(IOnyxNotices onyx, SetupNotices setup, ITextScrubber scrubber, TimeProvider clock)
{
    /// <summary>The Onyx row and the setup rows for <paramref name="caller"/>, in that order.</summary>
    /// <param name="caller">The caller.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The rows; empty for a session or when a source can't be read.</returns>
    public async ValueTask<IReadOnlyList<NeedsYouRow>> Read(CallerContext caller, CancellationToken cancellationToken) =>
        [.. await OnyxRows(caller, cancellationToken).ConfigureAwait(false), .. await SetupRows(caller, cancellationToken).ConfigureAwait(false)];

    // The setup rows that are computed, not folded: the human's to act on, so a session doesn't see them.
    private async ValueTask<IReadOnlyList<NeedsYouRow>> SetupRows(CallerContext caller, CancellationToken cancellationToken)
    {
        if (caller.Class != CallerClass.Human)
        {
            return [];
        }

        var repo = await setup.Repo(cancellationToken).ConfigureAwait(false);
        var recipes = await setup.Recipes(cancellationToken).ConfigureAwait(false);
        return repo is { } row ? [row, .. recipes] : recipes;
    }

    // The managed Onyx's row: it is the human's to act on, so a session doesn't see it.
    private async ValueTask<IReadOnlyList<NeedsYouRow>> OnyxRows(CallerContext caller, CancellationToken cancellationToken)
    {
        if (caller.Class != CallerClass.Human)
        {
            return [];
        }

        var notice = await onyx.Current(cancellationToken).ConfigureAwait(false);
        if (notice is not { IsSuccess: true, Value: { } found })
        {
            return [];
        }

        var row = NeedsYouRows.ForOnyx(found, clock.GetUtcNow(), scrubber);
        return row.IsSuccess ? [row.Value] : [];
    }
}
