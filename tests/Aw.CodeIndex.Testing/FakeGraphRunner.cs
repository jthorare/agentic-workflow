using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.Kernel;

namespace Aw.CodeIndex.Testing;

/// <summary>
/// A graph tool that answers what the test registered. Like the real runner it keeps only nodes that name a file of the request and only edges
/// between kept nodes, so a test sees the same hostile-input rule the adapter applies.
/// </summary>
public sealed class FakeGraphRunner : IGraphRunner
{
    private readonly List<GraphRequest> _requests = [];

    /// <summary>What the tool names; null runs as an empty graph.</summary>
    public GraphSnapshot? Graph { get; init; }

    /// <summary>The error <see cref="Probe"/> answers, or null for success.</summary>
    public Error? ProbeError { get; set; }

    /// <summary>The error <see cref="Run"/> answers, or null to answer the graph.</summary>
    public Error? RunError { get; set; }

    /// <summary>How many times <see cref="Run"/> was called.</summary>
    public int Runs => _requests.Count;

    /// <summary>The requests <see cref="Run"/> received, in order.</summary>
    public IReadOnlyList<GraphRequest> Requests => _requests;

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Probe(CancellationToken cancellationToken)
        => ValueTask.FromResult<Result<Unit>>(ProbeError is { } error ? error : Unit.Value);

    /// <inheritdoc />
    public ValueTask<Result<GraphSnapshot>> Run(GraphRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _requests.Add(request);
        if (RunError is { } error)
        {
            return ValueTask.FromResult<Result<GraphSnapshot>>(error);
        }

        var files = request.Files.Select(f => f.Path).ToHashSet(StringComparer.Ordinal);
        var graph = Graph ?? new GraphSnapshot([], []);
        var nodes = graph.Nodes.Where(n => n.File is { } f && files.Contains(f)).ToList();
        var keys = nodes.Select(n => n.NodeKey).ToHashSet(StringComparer.Ordinal);
        var edges = graph.Edges.Where(e => keys.Contains(e.Source) && keys.Contains(e.Target)).ToList();
        return ValueTask.FromResult<Result<GraphSnapshot>>(new GraphSnapshot(nodes, edges, graph.Dropped));
    }
}
