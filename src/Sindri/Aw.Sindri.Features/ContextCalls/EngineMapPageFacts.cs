using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Asks the engine for the current map's page facts and maps them to Core's plain form. Reads as the caller, so the deny cut applies and a session sees only its own repo.</summary>
/// <param name="scopes">The caller's scope.</param>
/// <param name="engine">The context engine.</param>
internal sealed class EngineMapPageFacts(ContextScopes scopes, IContextEngine engine) : IMapPageFacts
{
    public async ValueTask<Result<MapPageFacts>> Read(CancellationToken cancellationToken)
    {
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var facts = await engine.PageFacts(scope.Value, cancellationToken).ConfigureAwait(false);
        return facts.IsError
            ? facts.Error
            : new MapPageFacts(facts.Value.MapVersion, [.. facts.Value.Concepts.Select(static c => new MapConcept(c.Id.Value, c.Name))], facts.Value.Facts.SurfaceRepos, facts.Value.Facts.ActionRepos);
    }
}
