using System.Text;
using System.Text.Json;
using Aw.Context.Core.ConceptScan;
using Aw.Context.Infrastructure.Graph;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Kernel.Json;
using Microsoft.Data.Sqlite;

namespace Aw.Context.Infrastructure.ConceptScan;

/// <summary>
/// The concept hints in context.db (migration 0014_s5g_concept_hint), one row per version. A read opens the file read-only and never migrates it: no file, or a file from before the table, is a version with no hints.
/// A write migrates first and replaces the version's row in one transaction.
/// </summary>
internal sealed class SqliteConceptHintStore(ContextDatabase database) : IConceptHintStore
{
    private const string Upsert = "INSERT INTO map_concept_hint (version, scanned, hints) VALUES ($v, $scanned, $hints) ON CONFLICT (version) DO UPDATE SET scanned = excluded.scanned, hints = excluded.hints";
    private const string Select = "SELECT hints FROM map_concept_hint WHERE version = $v";
    private const string HasTable = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = 'map_concept_hint'";

    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(ConceptHintJsonContext.Default);

    public async ValueTask<Result<Unit>> Save(int version, IReadOnlyList<ConceptHint> hints, DateTimeOffset scanned, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hints);
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
            var json = Encoding.UTF8.GetString(AwJson.Serialize(hints.ToArray(), _json));
            var written = await SqliteQueries.Execute(begun.Value, new SqlStatement(Upsert, c => c.Bind("$v", version).Bind("$scanned", GraphText.Time(scanned)).Bind("$hints", json)), cancellationToken).ConfigureAwait(false);
            if (written.IsError)
            {
                return ContextDatabase.Translate(written.Error);
            }

            var committed = await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            return committed.IsError ? ContextDatabase.Translate(committed.Error) : Unit.Value;
        }
    }

    public async ValueTask<Result<IReadOnlyList<ConceptHint>>> Read(int version, CancellationToken cancellationToken)
    {
        var readable = await database.Readable(cancellationToken).ConfigureAwait(false);
        if (readable.IsError || readable.Value is not { } sessions)
        {
            return readable.IsError ? readable.Error : Results.Success<IReadOnlyList<ConceptHint>>([]);
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
                return tables.IsError ? ContextDatabase.Translate(tables.Error) : Results.Success<IReadOnlyList<ConceptHint>>([]);
            }

            var rows = await SqliteQueries.List(read.Value, new SqlStatement(Select, c => c.Bind("$v", version)), TextMapper.Instance, cancellationToken).ConfigureAwait(false);
            return rows.IsError ? ContextDatabase.Translate(rows.Error) : Parse(rows.Value);
        }
    }

    private static Result<IReadOnlyList<ConceptHint>> Parse(IReadOnlyList<string> rows)
    {
        if (rows.Count == 0)
        {
            return Results.Success<IReadOnlyList<ConceptHint>>([]);
        }

        var read = AwJson.Deserialize<ConceptHint[]>(Encoding.UTF8.GetBytes(rows[0]), _json);
        return read.IsError ? throw new InvalidOperationException($"map_concept_hint.hints isn't a hint list: {read.Error.Message}") : read.Value;
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
