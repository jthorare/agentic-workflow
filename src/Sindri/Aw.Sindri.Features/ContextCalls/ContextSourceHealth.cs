using Aw.Context.Core.Facade;
using Aw.Sindri.Core.Health;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// Reads each context source's progress the way GET /sindri/index/status does and maps every source that needs a look (<see cref="IndexSourceLook"/>)
/// to one health problem with its code (#4010). Nothing approved, or a scope or progress read that fails, adds no row: status never fails over the
/// context index.
/// </summary>
internal sealed class ContextSourceHealth(ContextScopes scopes, IContextEngine engine) : IContextSourceHealth
{
    public async ValueTask<IReadOnlyList<HealthProblem>> Problems(LoadedProfile? approved, CancellationToken cancellationToken)
    {
        if (approved is null)
        {
            return [];
        }

        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return [];
        }

        var progress = await engine.Progress(scope.Value, cancellationToken).ConfigureAwait(false);
        return progress.IsError ? [] : [.. progress.Value.Select(IndexSourceLook.Problem).OfType<HealthProblem>()];
    }
}
