using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Features.GetNeedsYou;

/// <summary>The human's rows that are read at request time and not folded from events: the setup rows (the repo step's failure and the MCP need). Every caller gets them, a session too.</summary>
/// <param name="runs">The run store: failed runs the owner has not opened.</param>
/// <param name="scrubber">Scrubs the text a failed run's row shows.</param>
/// <param name="setup">The setup rows that are computed (the repo step's failure, the 'setup:mcp' need).</param>
internal sealed class NeedsYouLiveRows(SetupNotices setup, IWorkflowRunStore runs, ITextScrubber scrubber)
{
    /// <summary>The setup rows.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The rows; empty when a source can't be read.</returns>
    public async ValueTask<IReadOnlyList<NeedsYouRow>> Read(CancellationToken cancellationToken) =>
        [.. await SetupRows(cancellationToken).ConfigureAwait(false), .. await FailedRunRows(runs, scrubber, cancellationToken).ConfigureAwait(false)];

    /// <summary>The most failed-run rows one read shows, the newest first.</summary>
    internal const int FailedRunLimit = 200;

    /// <summary>The rows of the live runs that ended failed and are unseen (Home spec 2026-11-12 §3.2, E1).</summary>
    /// <param name="runs">The run store.</param>
    /// <param name="scrubber">Scrubs the text a row shows.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One row per failed unseen run; empty when the store can't be read.</returns>
    internal static async ValueTask<IReadOnlyList<NeedsYouRow>> FailedRunRows(IWorkflowRunStore runs, ITextScrubber scrubber, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(scrubber);
        var failed = await runs.FailedUnseen(FailedRunLimit, cancellationToken).ConfigureAwait(false);
        return failed.IsError ? [] : [.. failed.Value.Select(run => NeedsYouRows.ForRunFailed(run, scrubber)).Where(static r => r.IsSuccess).Select(static r => r.Value)];
    }

    // The setup rows that are computed, not folded.
    private async ValueTask<IReadOnlyList<NeedsYouRow>> SetupRows(CancellationToken cancellationToken)
    {
        var repo = await setup.Repo(cancellationToken).ConfigureAwait(false);
        var mcp = await setup.Mcp(cancellationToken).ConfigureAwait(false);
        return [.. new[] { repo, mcp }.OfType<NeedsYouRow>()];
    }
}
