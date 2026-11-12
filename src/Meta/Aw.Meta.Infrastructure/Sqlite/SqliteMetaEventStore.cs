using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Meta.Core.Events;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Sqlite;

/// <summary>IMetaEventStore over meta.db (the meta_events table).</summary>
/// <param name="database">The meta database.</param>
internal sealed class SqliteMetaEventStore(MetaDatabase database) : IMetaEventStore
{
    public ValueTask<Result<long>> Append(MetaEvent metaEvent, CancellationToken cancellationToken)
    {
        var checkedEvent = MetaEventRules.Check(metaEvent);
        if (checkedEvent.IsError)
        {
            return ValueTask.FromResult(Results.Failure<long>(checkedEvent.Error));
        }

        var stored = checkedEvent.Value;
        return database.Write<long>(
            async work =>
            {
                var pruned = await SqliteQueries.Execute(work, MetaEventSql.Prune(stored.At - MetaEvent.Retention), cancellationToken).ConfigureAwait(false);
                if (pruned.IsError)
                {
                    return pruned.Error;
                }

                var inserted = await SqliteQueries.Execute(work, MetaEventSql.Insert(stored), cancellationToken).ConfigureAwait(false);
                if (inserted.IsError)
                {
                    return inserted.Error;
                }

                var id = await SqliteQueries.List(work, MetaEventSql.LastInsert, new RowMap<long>(static r => r.GetInt64(0)), cancellationToken).ConfigureAwait(false);
                return id.IsError ? id.Error : id.Value[0];
            },
            cancellationToken);
    }

    public ValueTask<Result<IReadOnlyList<MetaEvent>>> Read(long after, string? kind, string? lane, int max, CancellationToken cancellationToken) =>
        database.Query(MetaEventSql.Read(after, kind, lane, max), MetaEventSql.ReadEvent, cancellationToken);

    public ValueTask<Result<int>> Prune(DateTimeOffset olderThan, CancellationToken cancellationToken) =>
        database.Write(async work => await SqliteQueries.Execute(work, MetaEventSql.Prune(olderThan), cancellationToken).ConfigureAwait(false), cancellationToken);
}
