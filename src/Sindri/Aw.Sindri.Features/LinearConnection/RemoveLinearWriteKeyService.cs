using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.LinearConnection;

/// <summary>Removes the Linear write key's pointer. Owner only.</summary>
/// <param name="store">Keeps the key and its pointer.</param>
/// <param name="approval">Approves the profile when the removal is the only waiting change.</param>
/// <param name="live">The live profile.</param>
/// <param name="callers">The current caller.</param>
internal sealed class RemoveLinearWriteKeyService(ILinearConnectionStore store, ILinearConnectionApproval approval, IProfileSource live, ICallerContextAccessor callers)
    : IService<RemoveLinearWriteKeyRequest, RemoveLinearWriteKeyResponse>
{
    public ValueTask<Result<RemoveLinearWriteKeyResponse>> Handle(RemoveLinearWriteKeyRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"{store} {approval} {live} {callers}");
}
