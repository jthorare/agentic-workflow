using Aw.Context.Core.Facade;
using Aw.Context.Core.Linear;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>
/// The write gate over Context (Linear hub spec §5.1): a session never writes (SND-AUTH-002); the owner can once the write key resolves. A key that is not set, is refused or is the read key reads as SND-LINEAR-002;
/// Context failing reads as that error's own code. Which teams a write may touch (SND-LINEAR-003) comes with the write routes.
/// </summary>
/// <param name="scopes">The owner's scope.</param>
/// <param name="engine">Context's facade.</param>
internal sealed class ContextLinearWriteGate(LinearScopes scopes, IContextLinearWrites engine) : ILinearWriteGate
{
    public async ValueTask<LinearWriteGateView> Read(CancellationToken cancellationToken)
    {
        if (scopes.IsSession())
        {
            return new LinearWriteGateView(Allowed: false, "SND-AUTH-002");
        }

        var scope = await scopes.Read(owner: true, cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return new LinearWriteGateView(Allowed: false, scope.Error.Code);
        }

        var status = await engine.WriteKeyStatus(scope.Value, cancellationToken).ConfigureAwait(false);
        return status.IsError
            ? new LinearWriteGateView(Allowed: false, status.Error.Code)
            : status.Value is LinearWriteKeyResolves ? new LinearWriteGateView(Allowed: true, Code: null) : new LinearWriteGateView(Allowed: false, "SND-LINEAR-002");
    }
}
