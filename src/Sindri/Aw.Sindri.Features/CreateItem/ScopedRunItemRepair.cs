using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.CreateItem;

/// <summary>The engine's singleton <see cref="IRunItemRepair"/>: it runs <see cref="RunItemRepair"/>, which is scoped like the create-item handler, in a scope per call.</summary>
/// <param name="scopes">Makes the scope.</param>
internal sealed class ScopedRunItemRepair(IServiceScopeFactory scopes) : IRunItemRepair
{
    public async ValueTask<Result<int>> RepairAll(CancellationToken cancellationToken)
    {
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            return await scope.ServiceProvider.GetRequiredService<RunItemRepair>().RepairAll(cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask<Result<int>> RepairRun(WorkflowRunId run, CancellationToken cancellationToken)
    {
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            return await scope.ServiceProvider.GetRequiredService<RunItemRepair>().RepairRun(run, cancellationToken).ConfigureAwait(false);
        }
    }
}
