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
        var recorded = await RecordedApproval.Latest(ledger, events, cancellationToken).ConfigureAwait(false);
        var approved = ProfileRules.IsApproved(saved.IsSuccess ? saved.Value?.Hash : null, recorded?.Hash, loaded.Value.Hash);
        return new ValidateProfileResponse(loaded.Value.Hash, loaded.Value.Source, approved);
    }
}
