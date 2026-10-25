using Aw.Context.Core.Search;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Search;

/// <summary>Builds <see cref="SqliteMapSearchIndex"/>, an in-memory SQLite FTS5 index of one map's documents.</summary>
internal sealed class SqliteMapSearchIndexFactory : IMapSearchIndexFactory
{
    public Result<IMapSearchIndex> Build(IReadOnlyList<MapSearchDoc> docs)
    {
        ArgumentNullException.ThrowIfNull(docs);
        var built = SqliteMapSearchIndex.Create(docs);
        return built.IsError ? built.Error : Results.Success<IMapSearchIndex>(built.Value);
    }
}
