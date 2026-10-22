using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Challenges;
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
/// A, B records every step and a retry computes the same key. Approving redeems the 'approve profile &lt;hash6&gt;'
/// challenge (S1-D5) bound to the live hash; a missing, mistyped, expired or stale one is SND-PROFILE-004 and records nothing.
/// </summary>
internal sealed class ApproveProfileService(IProfileSource live, IApprovals approvals, HeldLease held, ILedgerStore ledger, EventFactory events, IChallenges challenges, ProfileChecks checks)
    : IService<ApproveProfileRequest, ApproveProfileResponse>
{
    public async ValueTask<Result<ApproveProfileResponse>> Handle(ApproveProfileRequest request, CancellationToken cancellationToken)
    {
        var loaded = await Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error;
        }

        var recorded = RecordedApproval.From(await ledger.Latest(EventTypes.ProfileApproved, cancellationToken).ConfigureAwait(false), events);
        if (recorded.IsError)
        {
            return recorded.Error;   // refuse: without the latest recorded approval, the preview can't judge and the key can't name what it follows
        }

        var preview = await Show(loaded.Value, recorded.Value, approving: request.Hash is not null, cancellationToken).ConfigureAwait(false);
        if (preview.IsError || request.Hash is not { } asked || preview.Value.Approved)
        {
            return preview;
        }

        var shown = preview.Value;

        if (Refusal(asked, loaded.Value, request.Answer) is { } refusal)
        {
            return refusal;
        }

        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var appended = await Record(asked, shown.Changes, recorded.Value, grant, cancellationToken).ConfigureAwait(false);
        var saved = appended.IsError ? appended.Error : await approvals.Save(loaded.Value, cancellationToken).ConfigureAwait(false);
        return saved.IsError ? saved.Error : shown with { Approved = true, Sequence = appended.Value };
    }

    /// <summary>
    /// Appends the approval of <paramref name="asked"/> unless the latest recorded approval already names it (the
    /// event exists and only the save failed), keyed by the event it follows.
    /// </summary>
    /// <returns>The approval event's sequence (the earlier one when it was already recorded).</returns>
    private async ValueTask<Result<long>> Record(ProfileHash asked, IReadOnlyList<string> changes, RecordedApproval? recorded,
        LeaseGrant grant, CancellationToken cancellationToken)
    {
        if (recorded?.Hash == asked)
        {
            return recorded.Sequence;
        }

        var entry = events.Entry(EventTypes.ProfileApproved, new ProfileApproved(asked, changes),
            string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ProfileApproved}:{asked.Value}:after:{recorded?.Sequence ?? 0}"));
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        if (appended.IsSuccess && events.CurrentCommandId() is { } command)
        {
            // setup-once spec §5.4: approving the profile is the consent to the automatic steps; a lost append only means the owner is asked once, by the "Turn on for your repos" row
            _ = await ledger.Append(events.Entry(EventTypes.SetupConsentGiven, new SetupConsentGiven(command, ["mcp", "map"], asked), "setup.consent.given:" + asked.Value), grant, cancellationToken).ConfigureAwait(false);
        }

        return appended.IsError ? appended.Error : appended.Value.Sequence;
    }

    /// <summary>
    /// What to show. Approved is <see cref="ProfileRules.IsApproved"/>: saved and the latest recorded approval, so a saved
    /// approval whose event is missing (or isn't the latest) shows as not approved and approving again records it.
    /// </summary>
    private async ValueTask<Result<ApproveProfileResponse>> Show(LoadedProfile loaded, RecordedApproval? recorded, bool approving, CancellationToken cancellationToken)
    {
        var latest = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (latest.IsError && !approving)
        {
            return latest.Error;   // a preview can't judge an approval it can't read; approving overwrites (and so repairs) it
        }

        var previous = latest.IsSuccess ? latest.Value : null;
        var approved = ProfileRules.IsApproved(previous?.Hash, recorded?.Hash, loaded.Hash);
        var changes = ProfileDiff.Between(previous?.Profile, loaded.Profile);
        return new ApproveProfileResponse(loaded.Hash, changes, approved, Sequence: null) { StepArgv = checks.StepArgv(loaded.Profile, changes) };
    }

    private Error? Refusal(ProfileHash asked, LoadedProfile live, ChallengeAnswer? answer) => true switch
    {
        _ when asked != live.Hash => SindriErrors.ProfileHashMismatch.With(
            $"{asked.Abbreviated}… isn't the live profile's hash ({live.Hash.Value}); run 'sindri profile approve' to see it and what changed"),
        _ when answer is null => SindriErrors.ProfileNeedsTerminal.With(
            $"approving needs you at an interactive terminal: run 'sindri profile approve <hash>' and type '{ChallengeAction.ApproveProfile(asked).Text}'"),
        _ => challenges.Redeem(answer, ChallengeAction.ApproveProfile(asked), ChallengeSubject.ForProfile(live.Hash), SindriErrors.ProfileNeedsTerminal) is { IsError: true } stale
            ? stale.Error
            : null,
    };

    /// <summary>The live profile, refused when its context section doesn't check, its repos included: an invalid section is never approved (SND-CONTEXT-001).</summary>
    private async ValueTask<Result<LoadedProfile>> Load(CancellationToken cancellationToken)
    {
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded;
        }

        return checks.Context(loaded.Value.Profile) is { } invalid ? invalid : loaded;
    }
}
