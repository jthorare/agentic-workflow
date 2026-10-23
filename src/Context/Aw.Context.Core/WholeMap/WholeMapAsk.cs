namespace Aw.Context.Core.WholeMap;

/// <summary>What one answer is built for: the map it reads, how much to send and which keys to place.</summary>
/// <param name="Basis">The map version the graph is.</param>
/// <param name="Detail">What was asked: auto, nodes or areas.</param>
/// <param name="Locate">The keys to place.</param>
/// <param name="Area">The key of one area to open on its own, or null.</param>
public sealed record WholeMapAsk(WholeMapBasis Basis, WholeMapDetail Detail, IReadOnlyList<string> Locate, string? Area = null);
