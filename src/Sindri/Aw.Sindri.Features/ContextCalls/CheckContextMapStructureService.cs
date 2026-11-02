using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Reads the structure measures through the engine (a session can't: SND-CONTEXT-020).</summary>
internal sealed class CheckContextMapStructureService(ContextScopes scopes, IContextEngine engine) : IService<CheckContextMapStructureRequest, MapChecksView>
{
    public async ValueTask<Result<MapChecksView>> Handle(CheckContextMapStructureRequest request, CancellationToken cancellationToken)
    {
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        return scope.IsError ? scope.Error : await engine.MapStructure(request.Source, progress: null, scope.Value, cancellationToken).ConfigureAwait(false);
    }
}
