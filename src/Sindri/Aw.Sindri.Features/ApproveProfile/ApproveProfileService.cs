using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.ApproveProfile;

/// <summary>
/// Shows or records an approval. The 'profile.approved' event is appended first and the snapshot saved second, so a
/// failed append changes nothing and a retry after a failed save finds the event already recorded and only saves. Each
/// approval's key names the event it follows ('profile.approved:&lt;hash&gt;:after:&lt;seq&gt;'), so approving A, B,
/// A, B records every step and a retry computes the same key.
/// </summary>
internal sealed class ApproveProfileService(IProfileSource live, IApprovals approvals, HeldLease held, ILedgerStore ledger, EventFactory events)
    : IService<ApproveProfileRequest, ApproveProfileResponse>
{
    public async ValueTask<Result<ApproveProfileResponse>> Handle(ApproveProfileRequest request, CancellationToken cancellationToken)
    {
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error;
        }

        var (shown, recorded) = await Show(loaded.Value, cancellationToken).ConfigureAwait(false);
        if (request.Hash is not { } asked || shown.Approved)
        {
            return shown;
        }

        if (Refusal(asked, loaded.Value, request.Confirmed) is { } refusal)
        {
            return refusal;
        }

        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var appended = await Record(asked, shown.Changes, recorded, grant, cancellationToken).ConfigureAwait(false);
        var saved = appended.IsError ? appended.Error : await approvals.Save(loaded.Value, cancellationToken).ConfigureAwait(false);
        return saved.IsError ? saved.Error : shown with { Approved = true };
    }

    /// <summary>
    /// Appends the approval of <paramref name="asked"/> unless the latest recorded approval already names it (the
    /// event exists and only the save failed), keyed by the event it follows.
    /// </summary>
    private async ValueTask<Result<Unit>> Record(ProfileHash asked, IReadOnlyList<string> changes, RecordedApproval? recorded,
        LeaseGrant grant, CancellationToken cancellationToken)
    {
        if (recorded?.Hash == asked)
        {
            return Unit.Value;
        }

        var entry = events.Entry(EventTypes.ProfileApproved, new ProfileApproved(asked, changes),
            string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ProfileApproved}:{asked.Value}:after:{recorded?.Sequence ?? 0}"));
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : Unit.Value;
    }

    /// <summary>
    /// What to show, and the latest recorded approval. Approved is <see cref="ProfileRules.IsApproved"/>: saved and the
    /// latest recorded approval, so a saved approval whose event is missing (or isn't the latest) shows as not approved
    /// and approving again records it.
    /// </summary>
    private async ValueTask<(ApproveProfileResponse Shown, RecordedApproval? Recorded)> Show(LoadedProfile loaded, CancellationToken cancellationToken)
    {
        var latest = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        var previous = latest.IsSuccess ? latest.Value : null;
        var recorded = await RecordedApproval.Latest(ledger, events, cancellationToken).ConfigureAwait(false);
        var approved = ProfileRules.IsApproved(previous?.Hash, recorded?.Hash, loaded.Hash);
        return (new ApproveProfileResponse(loaded.Hash, ProfileDiff.Between(previous?.Profile, loaded.Profile), approved), recorded);
    }

    private static Error? Refusal(ProfileHash asked, LoadedProfile live, bool confirmed) => true switch
    {
        _ when asked != live.Hash => SindriErrors.ProfileHashMismatch.With(
            $"{asked.Abbreviated}… isn't the live profile's hash ({live.Hash.Value}); run 'sindri profile approve' to see it and what changed"),
        _ when !confirmed => SindriErrors.ProfileNeedsTerminal.With(
            "approving needs you at an interactive terminal: run 'sindri profile approve <hash>' and type the first 6 characters"),
        _ => null,
    };
}
