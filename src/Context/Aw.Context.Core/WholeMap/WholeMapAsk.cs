namespace Aw.Context.Core.WholeMap;

/// <summary>What one answer is built for: the map it reads, how much to send and which keys to place.</summary>
/// <param name="Basis">The map version the graph is.</param>
/// <param name="Detail">What was asked: auto, nodes or areas.</param>
/// <param name="Locate">The keys to place.</param>
/// <param name="Area">The key of one area to open on its own, or null.</param>
public sealed record WholeMapAsk(WholeMapBasis Basis, WholeMapDetail Detail, IReadOnlyList<string> Locate, string? Area = null)
{
    /// <summary>The key of one concept to open on its own (its members and what lies two levels below them), or null; 'concept:other' opens the members no concept takes.</summary>
    public string? Concept { get; init; }

    /// <summary>The repos the whole map holds nodes of (counted over the map before any repo cut), for the page's repo picker.</summary>
    public IReadOnlyList<WholeMapRepo> Repos { get; init; } = [];

    /// <summary>Each node's repo by node key (<see cref="Graph.NodeRepos.Index"/> over the map before any repo cut), or null to leave the nodes' repos out.</summary>
    public IReadOnlyDictionary<string, string?>? RepoOf { get; init; }
}
