using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Features.SessionHeartbeat;

/// <summary>
/// The session's heartbeat (API spec §8.1): checks the launch secret, starts the 120 s clock again and, the first time a heartbeat carries the agent's own
/// session id, records it as 'launch.session-reported'.
/// </summary>
/// <param name="session">The session events and the secret check.</param>
internal sealed class SessionHeartbeatService(LaunchSessionEvents session) : IService<SessionHeartbeatRequest, SessionHeartbeatResponse>
{
    public async ValueTask<Result<SessionHeartbeatResponse>> Handle(SessionHeartbeatRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var proven = session.Verify(request.LaunchId, request.Secret);
        if (proven.IsError)
        {
            return proven.Error;
        }

        var beat = await session.Beat(request.LaunchId, request.SessionId, cancellationToken).ConfigureAwait(false);
        return beat.IsError ? beat.Error : new SessionHeartbeatResponse(request.LaunchId);
    }
}
