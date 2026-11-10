using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Features.ForgetMemoryFact;
using Aw.Sindri.Features.SearchMemory;
using Aw.Sindri.Features.WriteMemoryFact;

namespace Aw.Sindri.Features;

/// <summary>
/// <see cref="IGraphMemoryPort"/> over the memory handlers themselves, not their command chains: a memory node reads and writes the way 'sindri memory' does, as 'service:workflow'.
/// The node's run is already audited, and a workflow's owner approved it. Scoped, like the handlers.
/// </summary>
/// <param name="search">The recall handler.</param>
/// <param name="write">The write handler.</param>
/// <param name="forget">The forget handler.</param>
/// <param name="callers">Enters 'service:workflow' for the handlers.</param>
/// <param name="runs">Reads the run's item for the fact's provenance.</param>
internal sealed class WorkflowMemoryPort(SearchMemoryService search, WriteMemoryFactService write, ForgetMemoryFactService forget, ICallerContextAccessor callers, IWorkflowRunStore runs) : IGraphMemoryPort
{
    public ValueTask<Result<MemoryRecalled>> Recall(MemoryToRecall request, CancellationToken cancellationToken)
    {
        _ = (search, callers);
        throw new NotSupportedException();
    }

    public ValueTask<Result<MemoryRemembered>> Remember(MemoryToRemember request, CancellationToken cancellationToken)
    {
        _ = (write, runs);
        throw new NotSupportedException();
    }

    public ValueTask<Result<int>> Forget(MemoryToForget request, CancellationToken cancellationToken)
    {
        _ = forget;
        throw new NotSupportedException();
    }
}
