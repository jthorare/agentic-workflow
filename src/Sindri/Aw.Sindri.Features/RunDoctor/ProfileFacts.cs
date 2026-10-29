using Aw.Kernel;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.RunDoctor;

/// <summary>
/// Reads what the doctor's profile check judges: the live profile, the saved approval and the latest recorded one; and
/// the context rows that hang off the approved profile.
/// </summary>
internal sealed class ProfileFacts(IProfileSource live, IApprovals approvals, ILedgerStore ledger, EventFactory events, ContextEngineChecks context)
{
    public async ValueTask<(Result<LoadedProfile> Live, Result<LoadedProfile?> Saved, Result<RecordedApproval?> Recorded)> Read(CancellationToken cancellationToken)
    {
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        var saved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        var recorded = RecordedApproval.From(await ledger.Latest(EventTypes.ProfileApproved, cancellationToken).ConfigureAwait(false), events);
        return (loaded, saved, recorded);
    }

    /// <summary>The context engine's doctor rows: 'context:map', and 'context:retention' (S-3a).</summary>
    /// <param name="approved">The approved profile, or null (onyx is then 'not configured').</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The three checks.</returns>
    public ValueTask<DoctorCheck[]> Context(LoadedProfile? approved, CancellationToken cancellationToken) => context.Check(approved, cancellationToken);
}
