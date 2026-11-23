using Aw.Context.Core.Facade;
using Aw.Sindri.Core.Health;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// Reads each context source's progress the way GET /sindri/index/status does and maps every source that needs a look (<see cref="IndexSourceLook"/>)
/// to one health problem with its code (#4010). Nothing approved, or a scope or progress read that fails, adds no row: status never fails over the
/// context index. The first-pass limit counts the same run time the index status passes (zero while index-sync is paused), so the two never disagree.
/// </summary>
/// <param name="scopes">The profile's scope and the caller.</param>
/// <param name="engine">Context's facade.</param>
/// <param name="started">How long this serve has run.</param>
/// <param name="pauses">The paused services.</param>
internal sealed class ContextSourceHealth(ContextScopes scopes, IContextEngine engine, SourceSyncClock started, IServicePauses pauses) : IContextSourceHealth
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
        if (progress.IsError)
        {
            return [];
        }

        var running = await IndexSyncRunTime.For(started, pauses, cancellationToken).ConfigureAwait(false);
        return [.. progress.Value.Select(p => IndexSourceLook.Problem(p, running)).OfType<HealthProblem>()];
    }
}
