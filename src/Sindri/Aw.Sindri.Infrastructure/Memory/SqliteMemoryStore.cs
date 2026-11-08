using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Memory;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Infrastructure.Workflow;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Memory;

/// <summary>
/// IMemoryStore over the 'memory' and 'memory_fact' tables of ledger.db (migration 9): embeddings packed in the VectorCodec format, tags as sorted JSON.
/// A read never creates or migrates the file and finds nothing on a file older than the tables.
/// </summary>
/// <param name="database">The ledger file.</param>
internal sealed class SqliteMemoryStore(LedgerDatabase database) : IMemoryStore
{
    private readonly LedgerDatabase _database = database;

    public ValueTask<Result<Unit>> Create(MemoryInfo memory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(memory);
        return RecipeSessions.Write(_database, async ValueTask<Result<Unit>> (session, token) =>
        {
            var taken = await RecipeSessions.Exists(session, Named(MemorySql.MemoryExists, memory.Name), token).ConfigureAwait(false);
            if (taken.IsError)
            {
                return taken.Error;
            }

            if (taken.Value)
            {
                return SindriErrors.MemoryNameTaken.With($"A memory named {memory.Name} exists.");
            }

            var statement = new SqlStatement(MemorySql.InsertMemory, c => c
                .Bind("$name", memory.Name.Value).Bind("$description", memory.Description).Bind("$createdBy", memory.CreatedBy).Bind("$createdAt", memory.CreatedAt), $"memory {memory.Name}");
            var inserted = await RecipeSessions.Execute(session, statement, token).ConfigureAwait(false);
            return inserted.IsError ? inserted.Error : Unit.Value;
        }, cancellationToken);
    }

    public async ValueTask<Result<MemoryInfo>> Get(MemoryName name, CancellationToken cancellationToken)
    {
        var read = await RecipeSessions.Read<MemoryInfo?>(_database, LedgerDatabase.MemoryVersion, empty: null,
            (session, token) => RecipeSessions.First(session, Named(MemorySql.SelectMemory, name), MemoryInfoMapper.Instance, token), cancellationToken).ConfigureAwait(false);
        return read.IsError ? read.Error : read.Value is { } found ? found : NotFound(name);
    }

    public ValueTask<Result<IReadOnlyList<MemoryInfo>>> List(CancellationToken cancellationToken)
        => RecipeSessions.Read(_database, LedgerDatabase.MemoryVersion, [],
            (session, token) => RecipeSessions.List(session, new SqlStatement(MemorySql.SelectMemories), MemoryInfoMapper.Instance, token), cancellationToken);

    public ValueTask<Result<IReadOnlyDictionary<string, int>>> Counts(CancellationToken cancellationToken)
        => RecipeSessions.Read(_database, LedgerDatabase.MemoryVersion, new Dictionary<string, int>(StringComparer.Ordinal), async (session, token) =>
        {
            var rows = await RecipeSessions.List(session, new SqlStatement(MemorySql.SelectCounts), MemoryCountMapper.Instance, token).ConfigureAwait(false);
            return rows.IsError ? rows.Error : Results.Success<IReadOnlyDictionary<string, int>>(rows.Value.ToDictionary(static p => p.Key, static p => p.Value, StringComparer.Ordinal));
        }, cancellationToken);

    public async ValueTask<Result<IReadOnlyList<MemoryFact>>> Facts(MemoryName name, CancellationToken cancellationToken)
    {
        var read = await RecipeSessions.Read<IReadOnlyList<MemoryFact>?>(_database, LedgerDatabase.MemoryVersion, empty: null, async (session, token) =>
        {
            var exists = await RecipeSessions.Exists(session, Named(MemorySql.MemoryExists, name), token).ConfigureAwait(false);
            if (exists.IsError)
            {
                return exists.Error;
            }

            if (!exists.Value)
            {
                return Results.Success<IReadOnlyList<MemoryFact>?>(value: null);
            }

            var rows = await RecipeSessions.List(session, Named(MemorySql.SelectFacts, name), MemoryFactMapper.Instance, token).ConfigureAwait(false);
            return rows.IsError ? rows.Error : Results.Success<IReadOnlyList<MemoryFact>?>(rows.Value);
        }, cancellationToken).ConfigureAwait(false);
        return read.IsError ? read.Error : read.Value is { } facts ? Results.Success(facts) : NotFound(name);
    }

    public ValueTask<Result<Unit>> Add(MemoryFact fact, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fact);
        return RecipeSessions.Write(_database, async ValueTask<Result<Unit>> (session, token) =>
        {
            var exists = await RecipeSessions.Exists(session, Named(MemorySql.MemoryExists, fact.Store), token).ConfigureAwait(false);
            if (exists.IsError)
            {
                return exists.Error;
            }

            if (!exists.Value)
            {
                return NotFound(fact.Store);
            }

            var statement = new SqlStatement(MemorySql.InsertFact, c => BindFact(c, fact), $"fact {fact.Key}");
            var inserted = await RecipeSessions.Execute(session, statement, token).ConfigureAwait(false);
            return inserted.IsError ? inserted.Error : Unit.Value;
        }, cancellationToken);
    }

    public ValueTask<Result<bool>> Refresh(MemoryName memory, string key, DateTimeOffset refreshedAt, DateTimeOffset? expiresAt, CancellationToken cancellationToken)
        => RecipeSessions.Write(_database, async ValueTask<Result<bool>> (session, token) =>
        {
            var statement = new SqlStatement(MemorySql.Refresh, c => c
                .Bind("$name", memory.Value).Bind("$key", key).Bind("$refreshedAt", refreshedAt).Bind("$expiresAt", Iso(expiresAt)), $"fact {key}");
            var changed = await RecipeSessions.Execute(session, statement, token).ConfigureAwait(false);
            return changed.IsError ? changed.Error : changed.Value > 0;
        }, cancellationToken);

    public ValueTask<Result<int>> Delete(MemoryName memory, FactSelector selector, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return RecipeSessions.Write(_database, async ValueTask<Result<int>> (session, token) =>
        {
            var exists = await RecipeSessions.Exists(session, Named(MemorySql.MemoryExists, memory), token).ConfigureAwait(false);
            if (exists.IsError)
            {
                return exists.Error;
            }

            if (!exists.Value)
            {
                return NotFound(memory);
            }

            var statement = new SqlStatement(MemorySql.Delete, c => c
                .Bind("$name", memory.Value).Bind("$key", selector.Key).Bind("$tag", selector.Tag).Bind("$expiredAt", Iso(selector.ExpiredAt)), $"facts of {memory}");
            return await RecipeSessions.Execute(session, statement, token).ConfigureAwait(false);
        }, cancellationToken);
    }

    public ValueTask<Result<int>> DeleteExpired(DateTimeOffset now, CancellationToken cancellationToken)
        => RecipeSessions.Write(_database, (session, token) =>
            RecipeSessions.Execute(session, new SqlStatement(MemorySql.DeleteExpired, c => c.Bind("$now", now), Subject: "expired facts"), token), cancellationToken);

    private static SqlStatement Named(string sql, MemoryName name) => new(sql, c => c.Bind("$name", name.Value), $"memory {name}");

    private static Error NotFound(MemoryName name) => SindriErrors.MemoryNotFound.With($"No memory named {name}.");

    private static string? Iso(DateTimeOffset? value) => value?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private static SqliteCommand BindFact(SqliteCommand command, MemoryFact fact) => command
        .Bind("$key", fact.Key)
        .Bind("$name", fact.Store.Value)
        .Bind("$text", fact.Text)
        .Bind("$embedding", fact.Embedding.IsEmpty ? null : MemoryFactMapper.Encode(fact.Embedding.Span))
        .Bind("$tags", TagJson.Write(fact.Tags))
        .Bind("$workflow", fact.Provenance.Workflow)
        .Bind("$run", fact.Provenance.Run)
        .Bind("$item", fact.Provenance.Item)
        .Bind("$session", fact.Provenance.Session)
        .Bind("$expiresAt", Iso(fact.ExpiresAt))
        .Bind("$createdAt", fact.CreatedAt)
        .Bind("$refreshedAt", fact.RefreshedAt);
}
