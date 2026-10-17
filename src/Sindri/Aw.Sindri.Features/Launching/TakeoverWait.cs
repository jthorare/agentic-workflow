using Aw.Kernel;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Launching;

/// <summary>
/// Takes the item's active instances over before a session opens beside them (UI spec E-1, E-6). The launcher first waits for the item view to reach the
/// request, then sends every live instance the 'open-request' trigger (a no-op on one already taken over) and waits for each recorded result; it then
/// looks again at the ledger head, so an instance that started after the first look is taken over too. The whole wait is inside the request's deadline.
/// </summary>
/// <param name="views">The projector's progress.</param>
/// <param name="ledger">The ledger head.</param>
/// <param name="reader">Reads the item's instances.</param>
/// <param name="runtime">Sends the 'open-request' trigger.</param>
/// <param name="timings">The limits.</param>
internal sealed class TakeoverWait(IProjectorStatus views, ILedgerStore ledger, IViewReader reader, IInstanceRuntime runtime, LaunchTimings timings)
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

        var item = await reader.Get<ItemRow>(ViewNames.Items, pending.Request.Item.Value, cancellationToken).ConfigureAwait(false);
        if (item.IsError)
        {
            return false;
        }

        foreach (var address in Live(item.Value?.Engine))
        {
            var taken = await runtime.OpenRequest(address, pending.Request.Launch, cancellationToken).ConfigureAwait(false);
            if (taken.IsError)
            {
                return taken.Error;
            }
        }

        return true;
    }

    private static IEnumerable<InstanceAddress> Live(EngineItemPart? part)
    {
        if (part is null)
        {
            yield break;
        }

        foreach (var each in (part.Ended ? [] : new[] { part }).Concat(part.Earlier?.Where(static e => !e.Ended) ?? []))
        {
            if (InstanceAddress.TryFrom(each.Address, out var address))
            {
                yield return address;
            }
        }
    }
}
