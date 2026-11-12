using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.StartSession;

/// <summary>Starts a one-off session (agents spec §15.8). Red stub: the ports (profile, agent catalog, worktrees, ISessionHost, ledger) join the constructor in step 2.</summary>
internal sealed class StartSessionService : IService<StartSessionRequest, StartedSession>
{
    public ValueTask<Result<StartedSession>> Handle(StartSessionRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red: A7 step 2");
}
