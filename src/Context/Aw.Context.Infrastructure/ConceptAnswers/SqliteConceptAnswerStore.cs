using System.Globalization;
using System.Text;
using System.Text.Json;
using Aw.Context.Core.Concepts;
using Aw.Context.Core.Map;
using Aw.Context.Infrastructure.Graph;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Kernel.Json;
using Microsoft.Data.Sqlite;

namespace Aw.Context.Infrastructure.ConceptAnswers;

/// <summary>
/// The kept concepts and glossary answers in context.db (migration 0037_s5k_concept_answers). The basis is read in one read transaction from map_version, map_override and
/// stamp_intent, never from the graph rows. A write upserts one row, then drops the rows of versions the store no longer holds and all but the newest <see cref="Kept"/> rows.
/// No file is no basis (every read builds); a store this build can't read answers its error, and the reads build the answer then too.
/// </summary>
internal sealed class SqliteConceptAnswerStore(ContextDatabase database, TimeProvider clock) : IConceptAnswerStore
{
    // About one row per caller, repo and concepts page of the versions a person reads; the oldest past this go first.
    internal const int Kept = 200;

    private const string CurrentVersion = "SELECT id FROM map_version WHERE status = 'current'";
    private const string DraftVersion = "SELECT id FROM map_version WHERE status = 'draft' ORDER BY id DESC LIMIT 1";
    private const string HeldVersion = "SELECT id FROM map_version WHERE id = $v AND status IN ('current', 'retained')";
    private const string OverrideLayer = "SELECT count(*) || ':' || coalesce(max(rowid), 0) FROM map_override";
    private const string IntentCounter = "SELECT seq FROM stamp_intent WHERE id = 1";
    private const string Select = "SELECT body FROM concept_answer WHERE key = $k AND kind = $kind";
    private const string Upsert = "INSERT OR REPLACE INTO concept_answer (key, kind, version, basis, body, saved) VALUES ($k, $kind, $v, $basis, $body, $saved)";
    private const string DropGone = "DELETE FROM concept_answer WHERE version NOT IN (SELECT id FROM map_version)";
    private const string DropOldest = "DELETE FROM concept_answer WHERE rowid NOT IN (SELECT rowid FROM concept_answer ORDER BY rowid DESC LIMIT $n)";

    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(ConceptAnswerJsonContext.Default);

    public async ValueTask<Result<ConceptAnswerBasis?>> Basis(MapRead read, CancellationToken cancellationToken)
    {
        var readable = await database.Readable(cancellationToken).ConfigureAwait(false);
        if (readable.IsError || readable.Value is not { } sessions)
        {
            return readable.IsError ? readable.Error : Results.Success<ConceptAnswerBasis?>(value: null);
        }

        var begun = await sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return ContextDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var version = await SqliteQueries.List(begun.Value, Version(read), LongMapper.Instance, cancellationToken).ConfigureAwait(false);
            if (version.IsError || version.Value.Count == 0)
            {
                return version.IsError ? ContextDatabase.Translate(version.Error) : Results.Success<ConceptAnswerBasis?>(value: null);
            }

            var overrides = await SqliteQueries.One(begun.Value, new SqlStatement(OverrideLayer), TextMapper.Instance, cancellationToken).ConfigureAwait(false);
            var intent = overrides.IsError ? overrides.Error : await SqliteQueries.One(begun.Value, new SqlStatement(IntentCounter), LongMapper.Instance, cancellationToken).ConfigureAwait(false);
            return intent.IsError ? ContextDatabase.Translate(intent.Error) : new ConceptAnswerBasis((int)version.Value[0], overrides.Value, intent.Value);
        }
    }

    private static SqlStatement Version(MapRead read) =>
        read.Version is { } asked ? new SqlStatement(HeldVersion, c => c.Bind("$v", asked.Number)) : new SqlStatement(read.Draft ? DraftVersion : CurrentVersion);

    public async ValueTask<Result<KeptConcepts?>> Concepts(string key, CancellationToken cancellationToken)
    {
        var body = await Body(key, "concepts", cancellationToken).ConfigureAwait(false);
        return body.IsError ? body.Error : body.Value is { } text ? AwJson.Deserialize<StoredConcepts>(Encoding.UTF8.GetBytes(text), _json).Map<KeptConcepts?>(v => v.Kept()) : Results.Success<KeptConcepts?>(value: null);
    }

    public async ValueTask<Result<ConceptGlossaryView?>> Glossary(string key, CancellationToken cancellationToken)
    {
        var body = await Body(key, "glossary", cancellationToken).ConfigureAwait(false);
        return body.IsError ? body.Error : body.Value is { } text ? AwJson.Deserialize<ConceptGlossaryView>(Encoding.UTF8.GetBytes(text), _json).Map<ConceptGlossaryView?>(v => v) : Results.Success<ConceptGlossaryView?>(value: null);
    }

    public ValueTask<Result<Unit>> Keep(string key, ConceptAnswerBasis basis, KeptConcepts view, CancellationToken cancellationToken) =>
        Write(key, "concepts", basis, Encoding.UTF8.GetString(AwJson.Serialize(StoredConcepts.Of(view), _json)), cancellationToken);

    public ValueTask<Result<Unit>> Keep(string key, ConceptAnswerBasis basis, ConceptGlossaryView view, CancellationToken cancellationToken) =>
        Write(key, "glossary", basis, Encoding.UTF8.GetString(AwJson.Serialize(view, _json)), cancellationToken);

    private async ValueTask<Result<string?>> Body(string key, string kind, CancellationToken cancellationToken)
    {
        var readable = await database.Readable(cancellationToken).ConfigureAwait(false);
        if (readable.IsError || readable.Value is not { } sessions)
        {
            return readable.IsError ? readable.Error : Results.Success<string?>(value: null);
        }

        var begun = await sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return ContextDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var rows = await SqliteQueries.List(begun.Value, new SqlStatement(Select, c => c.Bind("$k", key).Bind("$kind", kind)), TextMapper.Instance, cancellationToken).ConfigureAwait(false);
            return rows.IsError ? ContextDatabase.Translate(rows.Error) : rows.Value.Count == 0 ? Results.Success<string?>(value: null) : rows.Value[0];
        }
    }

    private async ValueTask<Result<Unit>> Write(string key, string kind, ConceptAnswerBasis basis, string body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(basis);
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
            var stamp = basis.Overrides + "|" + basis.Intent.ToString(CultureInfo.InvariantCulture);
            var row = new SqlStatement(Upsert, c => c.Bind("$k", key).Bind("$kind", kind).Bind("$v", basis.Version).Bind("$basis", stamp).Bind("$body", body).Bind("$saved", GraphText.Time(clock.GetUtcNow())));
            var written = await SqliteQueries.Execute(begun.Value, row, cancellationToken).ConfigureAwait(false);
            var gone = written.IsError ? written.Error : await SqliteQueries.Execute(begun.Value, new SqlStatement(DropGone), cancellationToken).ConfigureAwait(false);
            var oldest = gone.IsError ? gone.Error : await SqliteQueries.Execute(begun.Value, new SqlStatement(DropOldest, c => c.Bind("$n", Kept)), cancellationToken).ConfigureAwait(false);
            if (oldest.IsError)
            {
                return ContextDatabase.Translate(oldest.Error);
            }

            var committed = await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            return committed.IsError ? ContextDatabase.Translate(committed.Error) : Unit.Value;
        }
    }

    private sealed class LongMapper : IRowMapper<long>
    {
        internal static LongMapper Instance { get; } = new();

        public long Map(SqliteDataReader reader) => reader.GetInt64(0);
    }

    private sealed class TextMapper : IRowMapper<string>
    {
        internal static TextMapper Instance { get; } = new();

        public string Map(SqliteDataReader reader) => reader.GetString(0);
    }
}
