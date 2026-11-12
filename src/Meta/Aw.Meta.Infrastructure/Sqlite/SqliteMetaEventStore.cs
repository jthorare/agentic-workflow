using Aw.Kernel;
using Aw.Meta.Core.Events;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Sqlite;

/// <summary>IMetaEventStore over meta.db (the meta_events table).</summary>
/// <param name="database">The meta database.</param>
internal sealed class SqliteMetaEventStore(MetaDatabase database) : IMetaEventStore
{
    public ValueTask<Result<long>> Append(MetaEvent metaEvent, CancellationToken cancellationToken)
    {
        _ = database;
        throw new NotSupportedException();
    }

    public ValueTask<Result<IReadOnlyList<MetaEvent>>> Read(long after, string? kind, string? lane, int max, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<int>> Prune(DateTimeOffset olderThan, CancellationToken cancellationToken) => throw new NotSupportedException();
}
