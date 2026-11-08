using Aw.Kernel;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.Launching;

/// <summary>
/// Waits for the views to reach a launch request before a session opens (UI spec E-1, E-6). It then looks again at the ledger head, so a later write is seen too. No workflow instance exists to take over now. The whole wait is inside the request's deadline.
/// </summary>
/// <param name="views">The projector's progress.</param>
/// <param name="ledger">The ledger head.</param>
/// <param name="timings">The limits.</param>
internal sealed class TakeoverWait(IProjectorStatus views, ILedgerStore ledger, LaunchTimings timings)
{
    /// <summary>Waits until no instance of the request's item runs automation beside the session that is about to open.</summary>
    /// <param name="pending">The request.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>True to launch; false to give up quietly (the views weren't ready in time, or the deadline passed: the sweep fails the request); an error when the engine refused.</returns>
    public async ValueTask<Result<bool>> Run(PendingLaunch pending, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pending);
        using var bound = new CancellationTokenSource(Remaining(pending), timings.Clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, bound.Token);
        try
        {
            var first = await Look(pending, pending.Sequence, linked.Token).ConfigureAwait(false);
            if (first.IsError || !first.Value)
            {
                return first;
            }

            var head = await ledger.Head(linked.Token).ConfigureAwait(false);
            return head.IsError ? false : await Look(pending, head.Value, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;   // the deadline passed while waiting: nothing was launched, and the sweep fails the request
        }
    }

    private TimeSpan Remaining(PendingLaunch pending)
    {
        var left = pending.At + timings.Deadline - timings.Clock.GetUtcNow();
        return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }

    private async ValueTask<Result<bool>> Look(PendingLaunch pending, long sequence, CancellationToken cancellationToken)
    {
        var waited = await views.WaitFor(sequence, Remaining(pending), cancellationToken).ConfigureAwait(false);
        if (waited.IsError)
        {
            return false;   // the views can't say whether automation runs, so nothing opens
        }

        return true;   // no workflow instance exists to take over; graph runs bring that back (v2 PR 6, 7)
    }
}
