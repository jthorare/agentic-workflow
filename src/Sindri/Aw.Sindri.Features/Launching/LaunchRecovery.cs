using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.Launching;

/// <summary>
/// The launcher's start-up sweep (UI spec E-1): a request recorded before this process started and still without an outcome had its claim lost to a
/// crash or a restart, so it fails as SND-LAUNCH-007 and is never retried; a started launch whose viewer was 'unconfirmed' and is still inside its
/// deadline is polled again. It reads the 'launches' view once that has reached the ledger head, so a stalled view holds the launcher back (it launches
/// nothing it cannot see) and the sweep runs on a later tick.
/// </summary>
/// <param name="ledger">The ledger head.</param>
/// <param name="views">The projector's progress.</param>
/// <param name="reader">Reads the launches.</param>
/// <param name="outcomes">Appends the failures.</param>
/// <param name="work">What the launcher remembers.</param>
/// <param name="timings">The limits.</param>
internal sealed class LaunchRecovery(ILedgerStore ledger, IProjectorStatus views, IViewReader reader, LaunchOutcomes outcomes, LaunchWork work, LaunchTimings timings)
{
    private static readonly TimeSpan _ready = TimeSpan.FromSeconds(2);

    /// <summary>Runs the sweep and moves the cursor to the head it swept.</summary>
    /// <param name="grant">The lease.</param>
    /// <param name="cancellationToken">Cancels the sweep.</param>
    /// <returns>Success, or what held it back.</returns>
    public async ValueTask<Result<Unit>> Run(LeaseGrant grant, CancellationToken cancellationToken)
    {
        var head = await ledger.Head(cancellationToken).ConfigureAwait(false);
        if (head.IsError)
        {
            return head.Error;
        }

        var ready = await views.WaitFor(head.Value, _ready, cancellationToken).ConfigureAwait(false);
        if (ready.IsError)
        {
            return ready.Error;
        }

        var rows = await reader.List<LaunchRow>(ViewNames.Launches, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        foreach (var row in rows.Value)
        {
            var handled = await Sweep(grant, row, cancellationToken).ConfigureAwait(false);
            if (handled.IsError)
            {
                return handled.Error;
            }
        }

        work.Cursor = head.Value;
        work.Recovered = true;
        return Unit.Value;
    }

    private async ValueTask<Result<Unit>> Sweep(LeaseGrant grant, LaunchRow row, CancellationToken cancellationToken)
    {
        if (row.State == LaunchState.Requested)
        {
            var failed = new LaunchFailed(row.Id, row.Item, SindriErrors.LaunchInterrupted.Code,
                "Sindri restarted before the launch started (fix: open the item again)", Stage: null);
            var appended = await outcomes.Failed(grant, failed, cancellationToken).ConfigureAwait(false);
            return appended.IsError ? appended.Error : Unit.Value;
        }

        if (row.Live && row is { Viewer.Outcome: ViewerOutcomeKind.Unconfirmed, Late: false, OutcomeAt: { } at } && at + timings.Deadline > timings.Clock.GetUtcNow())
        {
            work.Watch(new WatchedViewer(row.Id, row.Item, new HostRef(HostKind.Tmux, LaunchSessionName.For(row.Id)), at + timings.Deadline));
        }

        return Unit.Value;
    }
}
