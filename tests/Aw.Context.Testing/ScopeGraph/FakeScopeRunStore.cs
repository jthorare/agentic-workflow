using Aw.Context.Core.ScopeGraph;
using Aw.Kernel;

namespace Aw.Context.Testing.ScopeGraph;

/// <summary>The in-memory scope-run store; <see cref="ScopeRunStoreContract"/> holds it to the SQLite store's behavior.</summary>
public sealed class FakeScopeRunStore : IScopeRunStore
{
    private readonly Dictionary<string, HashSet<string>> _graphs = new(StringComparer.Ordinal);
    private readonly List<(string Instance, string Run, GraphRead Read)> _reads = [];

    /// <inheritdoc/>
    public ValueTask<Result<bool>> Remember(string instance, ScopeGraphView graph, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(graph);
        _graphs[instance] = graph.Nodes.Select(n => n.Id.Value).ToHashSet(StringComparer.Ordinal);
        return ValueTask.FromResult(Results.Success(value: true));
    }

    /// <inheritdoc/>
    public ValueTask<Result<bool?>> Holds(string instance, string node, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success<bool?>(_graphs.TryGetValue(instance, out var nodes) ? nodes.Contains(node) : null));

    /// <inheritdoc/>
    public ValueTask<Result<bool>> Log(string instance, string run, GraphRead read, CancellationToken cancellationToken)
    {
        _reads.Add((instance, run, read));
        return ValueTask.FromResult(Results.Success(value: true));
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<GraphRead>>> Reads(string instance, string run, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success<IReadOnlyList<GraphRead>>([.. _reads.Where(r => r.Instance == instance && r.Run == run).Select(r => r.Read)]));
}
