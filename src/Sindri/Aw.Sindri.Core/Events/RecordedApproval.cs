using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Events;

/// <summary>The latest 'profile.approved' event in the ledger: the hash it approved and its position.</summary>
/// <param name="Hash">The approved hash.</param>
/// <param name="Sequence">The event's ledger sequence.</param>
public sealed record RecordedApproval(ProfileHash Hash, long Sequence)
{
    /// <summary>
    /// Reads the latest recorded approval out of the ledger's answer for the latest 'profile.approved' event. A read
    /// error (or a payload that doesn't read) stays an error, so it is never mistaken for "nothing recorded".
    /// </summary>
    /// <param name="latest">The ledger's answer for the latest 'profile.approved' event.</param>
    /// <param name="events">Reads the event's payload.</param>
    /// <returns>The approval, null when none is recorded, or the error.</returns>
    public static Result<RecordedApproval?> From(Result<LedgerEvent?> latest, EventFactory events)
    {
        ArgumentNullException.ThrowIfNull(events);
        if (latest.IsError)
        {
            return latest.Error;
        }

        if (latest.Value is not { } recorded)
        {
            return Results.Success<RecordedApproval?>(value: null);
        }

        var payload = events.Payload<ProfileApproved>(recorded);
        return payload.IsError ? payload.Error : new RecordedApproval(payload.Value.Hash, recorded.Sequence);
    }
}
