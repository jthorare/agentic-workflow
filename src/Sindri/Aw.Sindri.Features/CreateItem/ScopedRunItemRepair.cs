using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Features.CreateItem;

/// <summary>The engine's singleton <see cref="IRunItemRepair"/>: it runs <see cref="RunItemRepair"/>, which is scoped like the create-item handler, in a scope per call.</summary>
/// <param name="inScope">Runs the work against a <see cref="RunItemRepair"/> from a new scope and disposes the scope after; made by the registration (ARCH-DI-001).</param>
internal sealed class ScopedRunItemRepair(Func<Func<RunItemRepair, ValueTask<Result<int>>>, ValueTask<Result<int>>> inScope) : IRunItemRepair
{
    public ValueTask<Result<int>> RepairAll(CancellationToken cancellationToken) => inScope(r => r.RepairAll(cancellationToken));

    public ValueTask<Result<int>> RepairRun(WorkflowRunId run, CancellationToken cancellationToken) => inScope(r => r.RepairRun(run, cancellationToken));
}
