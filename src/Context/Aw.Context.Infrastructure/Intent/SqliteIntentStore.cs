using System.Text;
using System.Text.Json;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Graph;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Kernel.Json;

namespace Aw.Context.Infrastructure.Intent;

/// <summary>
/// The intent layer in context.db (migration 0006_s5b_intent_links): what syncs wrote, outside the map versions. A page's records, links and cursor commit in one transaction, so a cursor never
/// moves past links that didn't commit and a replay writes nothing new.
/// </summary>
internal sealed class SqliteIntentStore(ContextDatabase database) : IIntentStore
{
    private readonly StampedCache<IntentLayer> _layers = new();

    private static readonly JsonSerializerOptions _keysJson = AwJson.CreateOptions(IntentJsonContext.Default);

    public async ValueTask<Result<IntentLayer>> Layer(CancellationToken cancellationToken)
    {
        var readable = await database.Readable(cancellationToken).ConfigureAwait(false);
        if (readable.IsError || readable.Value is not { } sessions)
        {
            return readable.IsError ? readable.Error : IntentLayer.Empty;
        }

        var read = await sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return ContextDatabase.Translate(read.Error);
        }

        await using (read.Value.ConfigureAwait(false))
        {
            var stamp = await StampedCache<IntentLayer>.Stamp(read.Value, StampedCache<IntentLayer>.IntentStamp, cancellationToken).ConfigureAwait(false);
            var layer = await _layers.GetOrLoad("layer", stamp, async () => (await Rows(read.Value, cancellationToken).ConfigureAwait(false)).Map<IntentLayer?>(l => l), cancellationToken).ConfigureAwait(false);
            return layer.IsError ? ContextDatabase.Translate(layer.Error) : layer.Value ?? IntentLayer.Empty;
        }
    }

    // The layer's rows, read in the caller's read transaction.
    private static async ValueTask<Result<IntentLayer>> Rows(SqliteReadSession session, CancellationToken cancellationToken)
    {
        var records = await SqliteQueries.List(session, new SqlStatement(IntentSql.Records), IntentMappers.Record, cancellationToken).ConfigureAwait(false);
        var links = records.IsError ? records.Error : await SqliteQueries.List(session, new SqlStatement(IntentSql.Links), IntentMappers.Link, cancellationToken).ConfigureAwait(false);
        var decisions = links.IsError ? links.Error : await SqliteQueries.List(session, new SqlStatement(IntentSql.Decisions), IntentMappers.Decision, cancellationToken).ConfigureAwait(false);
        var edges = decisions.IsError ? decisions.Error : await SqliteQueries.List(session, new SqlStatement(IntentSql.Edges), IntentMappers.Edge, cancellationToken).ConfigureAwait(false);
        var conflicts = edges.IsError ? edges.Error : await SqliteQueries.List(session, new SqlStatement(IntentSql.Conflicts), IntentMappers.Conflict, cancellationToken).ConfigureAwait(false);
        return conflicts.IsError ? conflicts.Error : new IntentLayer(records.Value, links.Value, decisions.Value, edges.Value) { Conflicts = conflicts.Value };
    }

    public async ValueTask<Result<IntentSourceState>> State(SourceId source, CancellationToken cancellationToken)
    {
        var readable = await database.Readable(cancellationToken).ConfigureAwait(false);
        if (readable.IsError || readable.Value is not { } sessions)
        {
            return readable.IsError ? readable.Error : new IntentSourceState(Cursor: null, LastSync: null, LastError: null, Records: 0, Links: 0, Decisions: 0, Queued: 0);
        }

        var read = await sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return ContextDatabase.Translate(read.Error);
        }

        await using (read.Value.ConfigureAwait(false))
        {
            var state = await SqliteQueries.One(read.Value, new SqlStatement(IntentSql.State, c => c.Bind("@source", source.Value)), IntentMappers.State, cancellationToken).ConfigureAwait(false);
            return state.IsError ? ContextDatabase.Translate(state.Error) : state.Value;
        }
    }

    public async ValueTask<Result<IntentCommit>> CommitPage(IntentPage page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        var writable = await database.Writable(cancellationToken).ConfigureAwait(false);
        if (writable.IsError)
        {
            return writable.Error;
        }

        var begun = await writable.Value.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return ContextDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var changed = 0;
            foreach (var statement in Statements(page))
            {
                var done = await SqliteQueries.Execute(begun.Value, statement, cancellationToken).ConfigureAwait(false);
                if (done.IsError)
                {
                    return ContextDatabase.Translate(done.Error);
                }

                changed += statement.Sql == IntentSql.UpsertRecord ? done.Value : 0;
            }

            var committed = await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            database.RestrictFiles();
            return committed.IsError ? ContextDatabase.Translate(committed.Error) : new IntentCommit(page.Records.Count, page.Links.Count, changed);
        }
    }

    public async ValueTask<Result<Unit>> RecordFailure(SourceId source, string code, CancellationToken cancellationToken)
    {
        var writable = await database.Writable(cancellationToken).ConfigureAwait(false);
        if (writable.IsError)
        {
            return writable.Error;
        }

        var begun = await writable.Value.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return ContextDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var done = await SqliteQueries.Execute(begun.Value, new SqlStatement(IntentSql.Failed, c => c.Bind("@source", source.Value).Bind("@code", code)), cancellationToken).ConfigureAwait(false);
            var committed = done.IsError ? done.Error : await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            return committed.IsError ? ContextDatabase.Translate(committed.Error) : Unit.Value;
        }
    }

    public async ValueTask<Result<IReadOnlyList<RecordKey>>> Queued(SourceId source, CancellationToken cancellationToken)
    {
        var readable = await database.Readable(cancellationToken).ConfigureAwait(false);
        if (readable.IsError || readable.Value is not { } sessions)
        {
            return readable.IsError ? readable.Error : Results.Success<IReadOnlyList<RecordKey>>([]);
        }

        var read = await sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return ContextDatabase.Translate(read.Error);
        }

        await using (read.Value.ConfigureAwait(false))
        {
            var keys = await SqliteQueries.List(read.Value, new SqlStatement(IntentSql.QueuedKeys, c => c.Bind("@source", source.Value)), IntentMappers.Key, cancellationToken).ConfigureAwait(false);
            return keys.IsError ? ContextDatabase.Translate(keys.Error) : Results.Success(keys.Value);
        }
    }

    public async ValueTask<Result<IReadOnlyList<RecordKey>>> LinkedUnder(SourceId source, int mapVersion, CancellationToken cancellationToken)
    {
        var readable = await database.Readable(cancellationToken).ConfigureAwait(false);
        if (readable.IsError || readable.Value is not { } sessions)
        {
            return readable.IsError ? readable.Error : Results.Success<IReadOnlyList<RecordKey>>([]);
        }

        var read = await sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return ContextDatabase.Translate(read.Error);
        }

        await using (read.Value.ConfigureAwait(false))
        {
            var keys = await SqliteQueries.List(read.Value, new SqlStatement(IntentSql.LinkedKeys, c => c.Bind("@source", source.Value).Bind("@map", mapVersion)), IntentMappers.Key, cancellationToken).ConfigureAwait(false);
            return keys.IsError ? ContextDatabase.Translate(keys.Error) : Results.Success(keys.Value);
        }
    }

    public ValueTask<Result<Unit>> SaveDecisions(DecisionBatch batch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        return Write(async (session, token) =>
        {
            var marked = await SqliteQueries.Execute(session, new SqlStatement(batch.Pending ? IntentSql.Touch : IntentSql.MarkExtracted, c => c.Bind("@record", batch.Record.Value).Bind("@version", batch.Version)), token).ConfigureAwait(false);
            if (marked.IsError || marked.Value == 0)
            {
                return marked.IsError ? marked.Error : Unit.Value;   // the record has a newer version now: these decisions are stale
            }

            foreach (var statement in Replacing(batch))
            {
                var done = await SqliteQueries.Execute(session, statement, token).ConfigureAwait(false);
                if (done.IsError)
                {
                    return done.Error;
                }
            }

            return Unit.Value;
        }, cancellationToken);
    }

    public ValueTask<Result<Unit>> Requeue(RecordKey record, string version, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        return Write(async (session, token) =>
        {
            var done = await SqliteQueries.Execute(session, new SqlStatement(IntentSql.Touch, c => c.Bind("@record", record.Value).Bind("@version", version)), token).ConfigureAwait(false);
            return done.IsError ? done.Error : Unit.Value;
        }, cancellationToken);
    }

    public ValueTask<Result<Unit>> SaveConflict(DecisionConflict conflict, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conflict);
        return Write(async (session, token) =>
        {
            var done = await SqliteQueries.Execute(session, new SqlStatement(IntentSql.InsertConflict, c => c.Bind("@a", conflict.A.Value).Bind("@b", conflict.B.Value)
                .Bind("@verdict", IntentMappers.Of(conflict.Verdict)).Bind("@at", conflict.At).Bind("@tries", conflict.Tries)), token).ConfigureAwait(false);
            return done.IsError ? done.Error : Unit.Value;
        }, cancellationToken);
    }

    private async ValueTask<Result<Unit>> Write(Func<ISqliteSession, CancellationToken, ValueTask<Result<Unit>>> work, CancellationToken cancellationToken)
    {
        var writable = await database.Writable(cancellationToken).ConfigureAwait(false);
        if (writable.IsError)
        {
            return writable.Error;
        }

        var begun = await writable.Value.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return ContextDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var done = await work(begun.Value, cancellationToken).ConfigureAwait(false);
            if (done.IsError)
            {
                return ContextDatabase.Translate(done.Error);
            }

            var committed = await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            database.RestrictFiles();
            return committed.IsError ? ContextDatabase.Translate(committed.Error) : Unit.Value;
        }
    }

    private static IEnumerable<SqlStatement> Replacing(DecisionBatch batch)
    {
        var keys = Encoding.UTF8.GetString(AwJson.Serialize(batch.Decisions.Select(d => d.ToKey().Value).ToArray(), _keysJson));
        yield return new SqlStatement(IntentSql.DropConflicts, c => c.Bind("@record", batch.Record.Value).Bind("@keys", keys));
        yield return new SqlStatement(IntentSql.DropEdges, c => c.Bind("@record", batch.Record.Value));
        yield return new SqlStatement(IntentSql.DropDecisions, c => c.Bind("@record", batch.Record.Value).Bind("@keys", keys));
        foreach (var decision in batch.Decisions)
        {
            yield return new SqlStatement(IntentSql.InsertDecision, c => c.Bind("@key", decision.ToKey().Value).Bind("@record", decision.Record.Value).Bind("@quote", decision.Quote)
                .Bind("@wording", decision.Wording).Bind("@provenance", GraphText.Of(decision.Provenance)).Bind("@at", decision.At ?? DateTimeOffset.UnixEpoch));
        }

        foreach (var edge in batch.Edges)
        {
            yield return new SqlStatement(IntentSql.InsertEdge, c => c.Bind("@from", edge.From.Value).Bind("@to", edge.To.Value).Bind("@type", GraphText.Of(edge.Type))
                .Bind("@provenance", GraphText.Of(edge.Provenance)).Bind("@confidence", edge.Confidence).Bind("@at", edge.At ?? DateTimeOffset.UnixEpoch));
        }
    }

    private static IEnumerable<SqlStatement> Statements(IntentPage page)
    {
        foreach (var record in page.Records)
        {
            yield return new SqlStatement(IntentSql.UpsertRecord, c => c.Bind("@key", record.Key.Value).Bind("@source", record.Source.Value).Bind("@version", record.Version)
                .Bind("@title", record.Title).Bind("@aliases", IntentMappers.Joined(record.Aliases)).Bind("@at", page.At).BindCreated(record.CreatedAt));
            yield return new SqlStatement(IntentSql.DeleteLinks, c => c.Bind("@record", record.Key.Value));
            if (page.MapVersion > 0)
            {
                yield return new SqlStatement(IntentSql.MarkLinked, c => c.Bind("@record", record.Key.Value).Bind("@map", page.MapVersion));
            }
        }

        foreach (var link in page.Links)
        {
            yield return new SqlStatement(IntentSql.InsertLink, c => c.Bind("@record", link.Record.Value).Bind("@target", link.Target.Value).Bind("@rule", IntentMappers.Of(link.Rule))
                .Bind("@confidence", link.Confidence).Bind("@span", link.SpanHash).Bind("@at", page.At));
        }

        yield return new SqlStatement(IntentSql.Synced, c => c.Bind("@source", page.Source.Value).Bind("@cursor", page.Cursor).Bind("@at", page.At));
    }
}
