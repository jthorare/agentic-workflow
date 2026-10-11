using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Events;

/// <summary>The latest 'profile.approved' event in the ledger: the hash it approved and its position.</summary>
/// <param name="Hash">The approved hash.</param>
/// <param name="Sequence">The event's ledger sequence.</param>
public sealed record RecordedApproval(ProfileHash Hash, long Sequence)
{
    /// <summary>Reads the latest recorded approval, or null when there is none or it can't be read.</summary>
    /// <param name="ledger">The ledger.</param>
    /// <param name="events">Reads the event's payload.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The approval, or null.</returns>
    public static async ValueTask<RecordedApproval?> Latest(ILedgerStore ledger, EventFactory events, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(events);
        var latest = await ledger.Latest(EventTypes.ProfileApproved, cancellationToken).ConfigureAwait(false);
        if (latest.IsError || latest.Value is not { } recorded)
        {
            return null;
        }

        var payload = events.Payload<ProfileApproved>(recorded);
        return payload.IsSuccess ? new RecordedApproval(payload.Value.Hash, recorded.Sequence) : null;
    }
}
