using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.StopSession;

/// <summary>Ends a one-off session Sindri started (agents spec §15.2).</summary>
internal sealed class StopSessionService : IService<StopSessionRequest, StoppedSession>
{
    public ValueTask<Result<StoppedSession>> Handle(StopSessionRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("StopSession is not built yet");
}
