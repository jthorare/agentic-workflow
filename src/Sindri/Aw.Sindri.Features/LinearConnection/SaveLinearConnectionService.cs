using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.LinearConnection;

/// <summary>Saves a pasted Linear key. Owner only.</summary>
/// <param name="checks">Asks Linear.</param>
/// <param name="store">Keeps the key and its pointer.</param>
/// <param name="approval">Approves the profile when the key is the only waiting change.</param>
/// <param name="live">The live profile.</param>
/// <param name="callers">The current caller.</param>
internal sealed class SaveLinearConnectionService(ILinearKeyChecks checks, ILinearConnectionStore store, ILinearConnectionApproval approval, IProfileSource live, ICallerContextAccessor callers)
    : IService<SaveLinearConnectionRequest, SaveLinearConnectionResponse>
{
    public ValueTask<Result<SaveLinearConnectionResponse>> Handle(SaveLinearConnectionRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"{checks} {store} {approval} {live} {callers}");
}
