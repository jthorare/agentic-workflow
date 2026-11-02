using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.ScopeGraph;
using Aw.Context.Features.GetScopeGraph;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features;

/// <summary>
/// The scoping graph of the facade and the run-scoped reads (issue 1005): build the graph, remember it per instance, check each context_* read of an agent Step run against it and log the read.
/// A read outside the graph is refused (154, not logged) unless it says it steps outside and why; an instance with no remembered graph answers 153 so the caller reads unscoped.
/// </summary>
internal sealed class ContextScopeGraph(IService<GetScopeGraphRequest, ScopeGraphView> build, IScopeRunStore store, TimeProvider clock)
{
    public ValueTask<Result<ScopeGraphView>> Build(ScopeGraphQuery query, ContextScope scope, CancellationToken cancellationToken) =>
        build.Handle(new GetScopeGraphRequest(query, scope), cancellationToken);

    public ValueTask<Result<bool>> Remember(string instance, ScopeGraphView graph, CancellationToken cancellationToken) =>
        store.Remember(instance, graph, clock.GetUtcNow(), cancellationToken);

    public ValueTask<Result<IReadOnlyList<GraphRead>>> Reads(string instance, string run, CancellationToken cancellationToken) =>
        store.Reads(instance, run, cancellationToken);

    public async ValueTask<Result<GraphRead>> Read(string instance, string run, GraphRead read, CancellationToken cancellationToken)
    {
        var held = await store.Holds(instance, read.Node, cancellationToken).ConfigureAwait(false);
        if (held.IsError)
        {
            return held.Error;
        }

        if (held.Value is not { } inside)
        {
            return ContextErrors.ScopeGraphUnavailable.With("this instance has no remembered scoping graph, so its reads are not scoped");
        }

        if (!inside && !(read.Outside && !string.IsNullOrWhiteSpace(read.Reason)))
        {
            return ContextErrors.NodeNotInScopeGraph.With($"'{read.Node}' is not in this run's scoping graph (fix: pick a node from graph.json, or call again with outside: true and a reason)");
        }

        var entry = read with { Outside = !inside, Reason = inside ? null : read.Reason };
        var logged = await store.Log(instance, run, entry, cancellationToken).ConfigureAwait(false);
        return logged.IsError ? logged.Error : entry;
    }
}
