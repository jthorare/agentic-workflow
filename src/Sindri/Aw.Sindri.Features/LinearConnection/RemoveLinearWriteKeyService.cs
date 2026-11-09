using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Linear;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.LinearConnection;

/// <summary>Removes the Linear write key's pointer from the live profile. Owner only. Removing a key that was never set is not an error.</summary>
/// <param name="store">Keeps the key and its pointer.</param>
/// <param name="live">The live profile, for its hash.</param>
/// <param name="callers">The current caller.</param>
/// <param name="log">Keeps the result and time of the last check.</param>
internal sealed class RemoveLinearWriteKeyService(ILinearConnectionStore store, IProfileSource live, ICallerContextAccessor callers, ILinearCheckStore log)
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

        _ = await log.ClearWrite(cancellationToken).ConfigureAwait(false);   // why: the pointer is gone, so a check of it must not outlive it
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        return loaded.IsError ? loaded.Error : new RemoveLinearWriteKeyResponse(Removed: true, loaded.Value.Hash.Value);
    }
}
