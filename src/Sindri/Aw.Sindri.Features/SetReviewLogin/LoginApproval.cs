using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.SetReviewLogin;

/// <summary>
/// The one-click approval of the login save (spec §0 step 1): when 'review.login' is the only key that differs from the approved profile, the save approves it, with the same
/// 'profile.approved' event and snapshot an approval records. When anything else waits, or nothing was ever approved, nothing is approved. It gives no setup consent: the
/// owner approved everything else already, and the login adds no step.
/// </summary>
/// <param name="approvals">The saved approvals.</param>
/// <param name="ledger">The ledger the approval event goes to.</param>
/// <param name="events">Builds the event.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="parser">Checks and hashes the approved profile with the login set.</param>
internal sealed class LoginApproval(IApprovals approvals, ILedgerStore ledger, EventFactory events, HeldLease held, IProfileTextParser parser)
{
    /// <summary>Approves <paramref name="live"/> when its only difference from the latest approval is the login.</summary>
    /// <param name="live">The live profile after the write.</param>
    /// <param name="login">The login just saved.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>True when the live profile is approved now; false when other changes wait; the error when an approval can't be read or written.</returns>
    public ValueTask<Result<bool>> Settle(LoadedProfile live, string login, CancellationToken cancellationToken) =>
        new ProfileChangeApproval(approvals, ledger, events, held).Settle(live, previous => IsApprovedPlusLogin(previous, live, login), cancellationToken);

    // The profile diff lists key paths and can miss a real change (an empty settings entry, a list value that joins to the same text), so the check is on the hash: the approved
    // profile with the login set must be exactly the live profile. The approval is a canonical snapshot with no YAML text, so the edit is made on the typed profile.
    private bool IsApprovedPlusLogin(LoadedProfile previous, LoadedProfile live, string login)
    {
        var expected = previous.Profile with { Review = (previous.Profile.Review ?? new PrReviewSettings()) with { Login = login } };
        var rechecked = parser.Recheck(expected, previous.Source);
        return rechecked.IsSuccess && rechecked.Value.Hash == live.Hash;
    }
}
