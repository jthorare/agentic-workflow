using Aw.Kernel;

namespace Aw.Context.Core.Search;

/// <summary>Builds the index for one map (a port: the adapter is an in-memory SQLite FTS5 table).</summary>
public interface IMapSearchIndexFactory
{
    /// <summary>Indexes <paramref name="docs"/>.</summary>
    /// <param name="docs">The documents, with ids 0 to n-1.</param>
    /// <returns>The index, or SND-CONTEXT-004 when it can't be built.</returns>
    public Result<IMapSearchIndex> Build(IReadOnlyList<MapSearchDoc> docs);
}
