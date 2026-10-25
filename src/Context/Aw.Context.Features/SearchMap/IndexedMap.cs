using Aw.Context.Core.Search;

namespace Aw.Context.Features.SearchMap;

/// <summary>One map's search index and the nodes behind its document ids.</summary>
/// <param name="Index">The index.</param>
/// <param name="Nodes">The nodes, by document id.</param>
internal sealed record IndexedMap(IMapSearchIndex Index, IReadOnlyList<IndexedNode> Nodes);
