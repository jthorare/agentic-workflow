using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.ValidateProfile;

internal sealed class ValidateProfileService(IProfileSource live, IApprovals approvals, ILedgerStore ledger, EventFactory events)
    : IService<ValidateProfileRequest, ValidateProfileResponse>
{
    public async ValueTask<Result<ValidateProfileResponse>> Handle(ValidateProfileRequest request, CancellationToken cancellationToken)
    {
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error;
        }

        var saved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (saved.IsError)
        {
            return saved.Error;   // an approval that can't be read isn't "not approved" either (doctor fails on it too)
        }

        var recorded = RecordedApproval.From(await ledger.Latest(EventTypes.ProfileApproved, cancellationToken).ConfigureAwait(false), events);
        if (recorded.IsError)
        {
            return recorded.Error;   // a ledger that can't be read isn't "not approved"
        }

        var approved = ProfileRules.IsApproved(saved.Value?.Hash, recorded.Value?.Hash, loaded.Value.Hash);
        return new ValidateProfileResponse(loaded.Value.Hash, loaded.Value.Source, approved);
    }
}
