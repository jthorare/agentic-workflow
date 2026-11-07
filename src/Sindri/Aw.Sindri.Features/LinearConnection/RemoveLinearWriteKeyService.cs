using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.LinearConnection;

/// <summary>Removes the Linear write key's pointer from the live profile, then approves the profile when that was the only change waiting. Owner only. Removing a key that was never set is not an error.</summary>
/// <param name="store">Keeps the key and its pointer.</param>
/// <param name="approval">Approves the profile when the removal is the only waiting change.</param>
/// <param name="live">The live profile, for its hash.</param>
/// <param name="callers">The current caller.</param>
internal sealed class RemoveLinearWriteKeyService(ILinearConnectionStore store, ILinearConnectionApproval approval, IProfileSource live, ICallerContextAccessor callers)
    : IService<RemoveLinearWriteKeyRequest, RemoveLinearWriteKeyResponse>
{
    public async ValueTask<Result<RemoveLinearWriteKeyResponse>> Handle(RemoveLinearWriteKeyRequest request, CancellationToken cancellationToken)
    {
        if (!LinearConnectionRules.IsOwner(callers))
        {
            return LinearConnectionRules.Refused(LinearConnectionRules.ChangeRefusal);
        }

        var removed = await store.RemoveWrite(cancellationToken).ConfigureAwait(false);
        if (removed.IsError)
        {
            return removed.Error;
        }

        var approved = await approval.Settle(cancellationToken).ConfigureAwait(false);
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        if (approved.IsError || loaded.IsError)
        {
            return approved.IsError ? approved.Error : loaded.Error;
        }

        return new RemoveLinearWriteKeyResponse(Removed: true, approved.Value, loaded.Value.Hash.Value);
    }
}
