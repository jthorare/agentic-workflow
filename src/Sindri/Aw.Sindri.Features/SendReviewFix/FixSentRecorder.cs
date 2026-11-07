using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.SendReviewFix;

/// <summary>Appends 'review.fix-sent' (PR review page spec §8.2 step 4), keyed by the item and the command so a retry is a row of its own.</summary>
/// <param name="held">The lease this process holds.</param>
/// <param name="ledger">Appends the event.</param>
/// <param name="events">Builds the entry.</param>
internal sealed class FixSentRecorder(HeldLease held, ILedgerStore ledger, EventFactory events)
{
    /// <summary>Appends the event.</summary>
    /// <param name="caller">The caller: the command id.</param>
    /// <param name="sent">The payload.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>Unit, or the error.</returns>
    internal async ValueTask<Result<Unit>> Record(CallerContext caller, ReviewFixSent sent, CancellationToken cancellationToken)
    {
        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var entry = events.Entry(EventTypes.ReviewFixSent, sent, $"{EventTypes.ReviewFixSent}:{sent.Item.Value}:{caller.CommandId.Value}", sent.Item.Value);
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : Unit.Value;
    }
}
