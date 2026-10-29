using System.Globalization;
using System.Text;
using System.Text.Json;
using Aw.Context.Core.ConceptSynthesis;
using Aw.Context.Infrastructure.Graph;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Kernel.Json;
using Microsoft.Data.Sqlite;

namespace Aw.Context.Infrastructure.ConceptSynthesis;

/// <summary>
/// The synthesized concepts in context.db (migration 0021_s5h_concept_synthesis). A read opens the file read-only and never migrates it: no file, or a file from before the tables, is no concepts and watermark 0.
/// A write migrates first and runs in one transaction.
/// </summary>
internal sealed class SqliteConceptSynthesisStore(ContextDatabase database) : IConceptSynthesisStore
{
    private const string Upsert = "INSERT INTO concept_synthesis (key, name, summary, members, membership_hash, state, watermark, created_at, updated_at) VALUES ($key, $name, $summary, $members, $hash, $state, $watermark, $created, $updated) "
        + "ON CONFLICT (key) DO UPDATE SET name = excluded.name, summary = excluded.summary, members = excluded.members, membership_hash = excluded.membership_hash, state = excluded.state, watermark = excluded.watermark, updated_at = excluded.updated_at";
    private const string Delete = "DELETE FROM concept_synthesis WHERE key = $key";
    private const string SelectAll = "SELECT key, name, summary, members, membership_hash, state, watermark, created_at, updated_at FROM concept_synthesis ORDER BY key";
    private const string UpsertCursor = "INSERT INTO concept_synthesis_cursor (id, watermark, updated_at) VALUES (1, $watermark, $updated) ON CONFLICT (id) DO UPDATE SET watermark = excluded.watermark, updated_at = excluded.updated_at";
    private const string SelectCursor = "SELECT watermark FROM concept_synthesis_cursor WHERE id = 1";
    private const string HasConcepts = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = 'concept_synthesis'";
    private const string HasCursor = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = 'concept_synthesis_cursor'";

    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(ConceptSynthesisJsonContext.Default);

    public ValueTask<Result<IReadOnlyList<SynthesizedConcept>>> All(CancellationToken cancellationToken) =>
        Read(HasConcepts, SelectAll, new ConceptMapper(), cancellationToken);

    public ValueTask<Result<Unit>> Save(SynthesizedConcept concept, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(concept);
        var members = Encoding.UTF8.GetString(AwJson.Serialize(concept.Members.ToArray(), _json));
        return Write(new SqlStatement(Upsert, c => c.Bind("$key", concept.Key).Bind("$name", concept.Name).Bind("$summary", concept.Summary).Bind("$members", members)
            .Bind("$hash", concept.MembershipHash).Bind("$state", concept.State == SynthesisState.Done ? "done" : "queued").Bind("$watermark", concept.Watermark)
            .Bind("$created", GraphText.Time(concept.CreatedAt)).Bind("$updated", GraphText.Time(concept.UpdatedAt))), cancellationToken);
    }

    public ValueTask<Result<Unit>> Remove(string key, CancellationToken cancellationToken) =>
        Write(new SqlStatement(Delete, c => c.Bind("$key", key)), cancellationToken);

    public async ValueTask<Result<long>> Watermark(CancellationToken cancellationToken)
    {
        var read = await Read(HasCursor, SelectCursor, new WatermarkMapper(), cancellationToken).ConfigureAwait(false);
        return read.IsError ? read.Error : read.Value.Count == 0 ? 0L : read.Value[0];
    }

    public ValueTask<Result<Unit>> SaveWatermark(long watermark, DateTimeOffset at, CancellationToken cancellationToken) =>
        Write(new SqlStatement(UpsertCursor, c => c.Bind("$watermark", watermark).Bind("$updated", GraphText.Time(at))), cancellationToken);

    private async ValueTask<Result<Unit>> Write(SqlStatement statement, CancellationToken cancellationToken)
    {
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
            var written = await SqliteQueries.Execute(begun.Value, statement, cancellationToken).ConfigureAwait(false);
            if (written.IsError)
            {
                return ContextDatabase.Translate(written.Error);
            }

            var committed = await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            return committed.IsError ? ContextDatabase.Translate(committed.Error) : Unit.Value;
        }
    }

    private async ValueTask<Result<IReadOnlyList<T>>> Read<T>(string tableCheck, string select, IRowMapper<T> mapper, CancellationToken cancellationToken)
    {
        var readable = await database.Readable(cancellationToken).ConfigureAwait(false);
        if (readable.IsError || readable.Value is not { } sessions)
        {
            return readable.IsError ? readable.Error : Results.Success<IReadOnlyList<T>>([]);
        }

        var read = await sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return ContextDatabase.Translate(read.Error);
        }

        await using (read.Value.ConfigureAwait(false))
        {
            var tables = await SqliteQueries.One(read.Value, new SqlStatement(tableCheck), CountMapper.Instance, cancellationToken).ConfigureAwait(false);
            if (tables.IsError || tables.Value == 0)
            {
                return tables.IsError ? ContextDatabase.Translate(tables.Error) : Results.Success<IReadOnlyList<T>>([]);
            }

            var rows = await SqliteQueries.List(read.Value, new SqlStatement(select), mapper, cancellationToken).ConfigureAwait(false);
            return rows.IsError ? ContextDatabase.Translate(rows.Error) : Results.Success(rows.Value);
        }
    }

    private sealed class CountMapper : IRowMapper<long>
    {
        internal static CountMapper Instance { get; } = new();

        public long Map(SqliteDataReader reader) => reader.GetInt64(0);
    }

    private sealed class WatermarkMapper : IRowMapper<long>
    {
        public long Map(SqliteDataReader reader) => reader.GetInt64(0);
    }

    private sealed class ConceptMapper : IRowMapper<SynthesizedConcept>
    {
        public SynthesizedConcept Map(SqliteDataReader reader)
        {
            var members = AwJson.Deserialize<ConceptMember[]>(Encoding.UTF8.GetBytes(reader.GetString(3)), _json);
            if (members.IsError)
            {
                throw new InvalidOperationException($"concept_synthesis.members isn't a member list: {members.Error.Message}");
            }

            return new SynthesizedConcept(reader.GetString(0), reader.GetString(1), reader.GetString(2), members.Value, reader.GetString(4),
                reader.GetString(5) == "done" ? SynthesisState.Done : SynthesisState.Queued, reader.GetInt64(6), Time(reader.GetString(7)), Time(reader.GetString(8)));
        }

        private static DateTimeOffset Time(string text) => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
    }
}
