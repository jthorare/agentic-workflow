using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.LinearConnection;

/// <summary>Reads where the Linear connection stands. Owner only: a session is refused SND-AUTH-002 before anything is read.</summary>
/// <param name="facts">The connection's facts.</param>
/// <param name="callers">The current caller.</param>
internal sealed class GetLinearConnectionService(ILinearConnectionFacts facts, ICallerContextAccessor callers) : IService<GetLinearConnectionRequest, LinearConnectionView>
{
    public ValueTask<Result<LinearConnectionView>> Handle(GetLinearConnectionRequest request, CancellationToken cancellationToken) =>
        LinearConnectionRules.IsOwner(callers)
            ? facts.Read(cancellationToken)
            : ValueTask.FromResult<Result<LinearConnectionView>>(LinearConnectionRules.Refused(LinearConnectionRules.ReadRefusal));
}
