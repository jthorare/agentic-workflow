using Aw.Context.Core.Extract;
using Aw.Context.Infrastructure.Graph;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Context.Infrastructure.Labeling;

/// <summary>
/// The labeler's cache in context.db (migration 0024_label_cache). A read opens the file read-only and never migrates it: no file, or a file from before the table, is all misses.
/// A write migrates first and upserts every label in one transaction.
/// </summary>
internal sealed class SqliteLabelCache(ContextDatabase database, TimeProvider clock) : ILabelCache
{
    private const string Upsert = "INSERT INTO label_cache (key, label, saved) VALUES ($k, $label, $saved) ON CONFLICT (key) DO UPDATE SET label = excluded.label, saved = excluded.saved";
    private const string Select = "SELECT label FROM label_cache WHERE key = $k";
    private const string HasTable = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = 'label_cache'";

    public async ValueTask<Result<IReadOnlyDictionary<string, string>>> Find(IReadOnlyList<string> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        var readable = await database.Readable(cancellationToken).ConfigureAwait(false);
        if (readable.IsError || readable.Value is not { } sessions)
        {
            return readable.IsError ? readable.Error : Results.Success<IReadOnlyDictionary<string, string>>(found);
        }

        var read = await sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return ContextDatabase.Translate(read.Error);
        }

        await using (read.Value.ConfigureAwait(false))
        {
            var tables = await SqliteQueries.One(read.Value, new SqlStatement(HasTable), CountMapper.Instance, cancellationToken).ConfigureAwait(false);
            if (tables.IsError || tables.Value == 0)
            {
                return tables.IsError ? ContextDatabase.Translate(tables.Error) : Results.Success<IReadOnlyDictionary<string, string>>(found);
            }

            var looked = await Lookup(read.Value, keys, found, cancellationToken).ConfigureAwait(false);
            if (looked.IsError)
            {
                return looked.Error;
            }

            return found;
        }
    }

    private static async ValueTask<Result<Unit>> Lookup(ISqliteSession session, IReadOnlyList<string> keys, Dictionary<string, string> found, CancellationToken cancellationToken)
    {
        foreach (var key in keys.Distinct(StringComparer.Ordinal))
        {
            var rows = await SqliteQueries.List(session, new SqlStatement(Select, c => c.Bind("$k", key)), TextMapper.Instance, cancellationToken).ConfigureAwait(false);
            if (rows.IsError)
            {
                return ContextDatabase.Translate(rows.Error);
            }

            if (rows.Value.Count > 0)
            {
                found[key] = rows.Value[0];
            }
        }

        return Unit.Value;
    }

    public async ValueTask<Result<Unit>> Save(IReadOnlyList<KeyValuePair<string, string>> labels, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(labels);
        if (labels.Count == 0)
        {
            return Unit.Value;
        }

        var open = await database.Writable(cancellationToken).ConfigureAwait(false);
        if (open.IsError)
        {
            return open.Error;
        }

        var begun = await open.Value.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return ContextDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var saved = GraphText.Time(clock.GetUtcNow());
            foreach (var (key, label) in labels)
            {
                var written = await SqliteQueries.Execute(begun.Value, new SqlStatement(Upsert, c => c.Bind("$k", key).Bind("$label", label).Bind("$saved", saved)), cancellationToken).ConfigureAwait(false);
                if (written.IsError)
                {
                    return ContextDatabase.Translate(written.Error);
                }
            }

            var committed = await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            return committed.IsError ? ContextDatabase.Translate(committed.Error) : Unit.Value;
        }
    }

    private sealed class CountMapper : IRowMapper<long>
    {
        internal static CountMapper Instance { get; } = new();

        public long Map(SqliteDataReader reader) => reader.GetInt64(0);
    }

    private sealed class TextMapper : IRowMapper<string>
    {
        internal static TextMapper Instance { get; } = new();

        public string Map(SqliteDataReader reader) => reader.GetString(0);
    }
}
