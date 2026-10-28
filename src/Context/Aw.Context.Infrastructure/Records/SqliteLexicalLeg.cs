using System.Text.Json;
using Aw.Context.Core.Audience;
using Aw.Context.Core.Records;
using Aw.Context.Core.Search;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Data;

namespace Aw.Context.Infrastructure.Records;

/// <summary>
/// The lexical leg over 'chunk_fts' (spec §6.1): every term a quoted phrase joined with OR, ranked by bm25, with the audience mask inside the statement, then the best
/// chunk of each record. A store with no chunk at all (never synced) answers like no match, with no degraded line, because that is the normal state of a fresh box; a scope that lets nothing through answers like no match.
/// </summary>
internal sealed class SqliteLexicalLeg(ContextDatabase database) : ILexicalLeg
{
    private static readonly LexicalAnswer _none = new([], Degraded: null);

    public async ValueTask<Result<LexicalAnswer>> Search(IReadOnlyList<string> terms, AudienceScope scope, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 1000);
        var match = LexicalMatch.Of(terms);
        var readable = await database.Readable(cancellationToken).ConfigureAwait(false);
        if (readable.IsError)
        {
            return readable.Error;
        }

        if (readable.Value is not { } sessions)
        {
            return Empty();
        }

        var read = await sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return ContextDatabase.Translate(read.Error);
        }

        await using (read.Value.ConfigureAwait(false))
        {
            var any = await SqliteQueries.List(read.Value, new SqlStatement(RecordSql.AnyChunk), LexicalRow.Exists, cancellationToken).ConfigureAwait(false);
            if (any.IsError)
            {
                return ContextDatabase.Translate(any.Error);
            }

            if (any.Value.Count == 0 || !any.Value[0])
            {
                return Empty();
            }

            return await Match(read.Value, match, scope, limit, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask<Result<LexicalAnswer>> Match(SqliteReadSession session, string? match, AudienceScope scope, int limit, CancellationToken cancellationToken)
    {
        if (match is null || (scope.Allowed.Count == 0 && scope.Records.Count == 0))
        {
            return _none;
        }

        var rows = await SqliteQueries.List(session, new SqlStatement(RecordSql.LexicalSearch, c => c
                .Bind("@match", match)
                .Bind("@audiences", Array(scope.Allowed.Select(k => k.Value)))
                .Bind("@records", Array(scope.Records.Select(k => k.Value)))
                .Bind("@limit", limit)), LexicalRow.Mapper, cancellationToken).ConfigureAwait(false);
        return rows.IsError ? ContextDatabase.Translate(rows.Error) : new LexicalAnswer(BestPerRecord(rows.Value), Degraded: null);
    }

    private static Result<LexicalAnswer> Empty() => _none;

    /// <summary>Rows arrive best first, so the first chunk seen of a record is its best.</summary>
    private static List<RecordHit> BestPerRecord(IReadOnlyList<LexicalRow> rows)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var hits = new List<RecordHit>();
        foreach (var row in rows)
        {
            if (ChunkKey.TryParse(row.Chunk, out var chunk) && seen.Add(chunk.Record.Value))
            {
                hits.Add(new RecordHit(chunk, row.Title, row.LineStart, row.LineEnd, -row.Bm25));
            }
        }

        return hits;
    }

    private static string Array(IEnumerable<string> values)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartArray();
            foreach (var key in values.Order(StringComparer.Ordinal))
            {
                json.WriteStringValue(key);
            }

            json.WriteEndArray();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }
}
