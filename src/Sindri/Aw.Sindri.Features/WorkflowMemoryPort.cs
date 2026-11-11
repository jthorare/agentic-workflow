using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Memory;
using Aw.Sindri.Features.ForgetMemoryFact;
using Aw.Sindri.Features.SearchMemory;
using Aw.Sindri.Features.WriteMemoryFact;

namespace Aw.Sindri.Features;

/// <summary>
/// <see cref="IGraphMemoryPort"/> over the memory handlers themselves, not their command chains: a memory node reads and writes the way 'sindri memory' does, as 'service:workflow'.
/// The node's run is already audited, and a workflow's owner approved it. Scoped, like the handlers: <see cref="ScopedMemoryPort"/> is the engine's singleton port and runs this one in a scope per call.
/// It sits beside the slices, not in one, because it joins three of them.
/// </summary>
/// <param name="search">The recall handler.</param>
/// <param name="write">The write handler.</param>
/// <param name="forget">The forget handler.</param>
/// <param name="callers">Enters 'service:workflow' for the handlers.</param>
/// <param name="runs">Reads the run's item for the fact's provenance.</param>
/// <param name="time">The clock a fact's expiry counts from.</param>
internal sealed class WorkflowMemoryPort(SearchMemoryService search, WriteMemoryFactService write, ForgetMemoryFactService forget, ICallerContextAccessor callers, IWorkflowRunStore runs, TimeProvider time) : IGraphMemoryPort
{
    public async ValueTask<Result<MemoryRecalled>> Recall(MemoryToRecall request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var entered = callers.EnterService("workflow");
        var found = await search.Handle(new SearchMemoryRequest(request.Memory, request.Query, request.Level, request.Limit), cancellationToken).ConfigureAwait(false);
        return found.IsError ? found.Error : new MemoryRecalled([.. found.Value.Facts.Select(static f => f.Text)], found.Value.Mode);
    }

    public async ValueTask<Result<MemoryRemembered>> Remember(MemoryToRemember request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        // why: the item is the run's own, set by whoever started it; a run that can't be read leaves the fact without one, with a warning, rather than failing a node whose work is done
        var run = await runs.Get(request.Run, cancellationToken).ConfigureAwait(false);
        var item = run.IsSuccess ? run.Value.ItemId?.Value : null;
        var provenance = new MemoryProvenance(request.Run.Value[..request.Run.Value.IndexOf('@', StringComparison.Ordinal)], request.Run.Value, item, Session: null);
        DateTimeOffset? expiresAt = request.ExpiresIn is { } span ? time.GetUtcNow() + span : null;
        using var entered = callers.EnterService("workflow");
        var written = await write.Handle(new WriteMemoryFactRequest(request.Memory, request.Fact, request.Tags, expiresAt, provenance), cancellationToken).ConfigureAwait(false);
        var warning = run.IsError ? $"the fact was written, but run {request.Run.Value} could not be read, so the fact carries no item ({run.Error.Message})" : null;
        return written.IsError ? written.Error : new MemoryRemembered(written.Value.Key, written.Value.Action, warning);
    }

    public async ValueTask<Result<int>> Forget(MemoryToForget request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var entered = callers.EnterService("workflow");
        var forgotten = await forget.Handle(new ForgetMemoryFactRequest(request.Memory, request.Fact, request.Tag, Expired: false), cancellationToken).ConfigureAwait(false);
        return forgotten.IsError ? forgotten.Error : forgotten.Value.Deleted;
    }
}
