using Aw.CodeIndex.Core.Facade;

namespace Aw.CodeIndex.Core.Similarity;

/// <summary>What Similar finds for a symbol, before the facade stamps it.</summary>
/// <param name="Exact">Symbols with the subject's shape hash.</param>
/// <param name="Near">Banded candidates whose estimated Jaccard similarity reaches the threshold, closest first.</param>
public sealed record SimilarClones(IReadOnlyList<SymbolHit> Exact, IReadOnlyList<NearClone> Near);
