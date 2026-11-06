using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
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
internal sealed class LoginApproval(IApprovals approvals, ILedgerStore ledger, EventFactory events, HeldLease held)
{
    private const string Key = "review.login";

    /// <summary>Approves <paramref name="live"/> when its only difference from the latest approval is the login.</summary>
    /// <param name="live">The live profile after the write.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>True when the live profile is approved now; false when other changes wait; the error when an approval can't be read or written.</returns>
    public async ValueTask<Result<bool>> Settle(LoadedProfile live, CancellationToken cancellationToken)
    {
        var latest = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        var recorded = RecordedApproval.From(await ledger.Latest(EventTypes.ProfileApproved, cancellationToken).ConfigureAwait(false), events);
        if (latest.IsError || recorded.IsError)
        {
            return latest.IsError ? latest.Error : recorded.Error;
        }

        if (ProfileRules.IsApproved(latest.Value?.Hash, recorded.Value?.Hash, live.Hash))
        {
            return true;
        }

        if (latest.Value is not { } previous || !ProfileDiff.Between(previous.Profile, live.Profile).SequenceEqual([Key], StringComparer.Ordinal))
        {
            return false;
        }

        return await Approve(live, previous, recorded.Value, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<bool>> Approve(LoadedProfile live, LoadedProfile previous, RecordedApproval? recorded, CancellationToken cancellationToken)
    {
        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        if (recorded?.Hash != live.Hash)
        {
            var entry = events.Entry(EventTypes.ProfileApproved, new ProfileApproved(live.Hash, ProfileDiff.Between(previous.Profile, live.Profile)),
                string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ProfileApproved}:{live.Hash.Value}:after:{recorded?.Sequence ?? 0}"));
            var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
            if (appended.IsError)
            {
                return appended.Error;
            }
        }

        var saved = await approvals.Save(live, cancellationToken).ConfigureAwait(false);
        return saved.IsError ? saved.Error : true;
    }
}
