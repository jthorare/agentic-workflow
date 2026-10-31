using Aw.Context.Core.Facade;
using Aw.Context.Core.Pages;
using Aw.Kernel;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Asks the engine for the current map's page facts. Reads as the caller, so the deny cut applies and a session sees only its own repo.</summary>
/// <param name="scopes">The caller's scope.</param>
/// <param name="engine">The context engine.</param>
internal sealed class EngineMapPageFacts(ContextScopes scopes, IContextEngine engine) : IMapPageFacts
{
    public async ValueTask<Result<PageMapFacts>> Read(CancellationToken cancellationToken)
    {
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        return scope.IsError ? scope.Error : await engine.PageFacts(scope.Value, cancellationToken).ConfigureAwait(false);
    }
}
