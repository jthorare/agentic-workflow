using System.Globalization;
using System.Text;
using System.Text.Json;
using Aw.Context.Core.Surfaces;
using Aw.Context.Infrastructure.Graph;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Kernel.Json;
using Microsoft.Data.Sqlite;

namespace Aw.Context.Infrastructure.Surfaces;

/// <summary>
/// Jev's product-surface decisions in context.db (migration 0025_s5j_surface_pick). A read opens the file read-only and never migrates it: no file, or a file from before the table, is all misses.
/// A write migrates first and upserts every decision in one transaction.
/// </summary>
internal sealed class SqliteSurfacePickStore(ContextDatabase database) : ISurfacePickStore
{
    private const string Upsert = "INSERT INTO surface_pick (group_key, choice, survivor_key, merged_name, candidate_keys, model, decided_at) VALUES ($k, $choice, $survivor, $name, $candidates, $model, $decided) "
        + "ON CONFLICT (group_key) DO UPDATE SET choice = excluded.choice, survivor_key = excluded.survivor_key, merged_name = excluded.merged_name, candidate_keys = excluded.candidate_keys, model = excluded.model, decided_at = excluded.decided_at";
    private const string Select = "SELECT group_key, choice, survivor_key, merged_name, candidate_keys, model, decided_at FROM surface_pick WHERE group_key = $k";
    private const string HasTable = "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = 'surface_pick'";

    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(SurfacePickJsonContext.Default);

    public async ValueTask<Result<IReadOnlyDictionary<string, SurfaceDecision>>> Get(IReadOnlyCollection<string> groupKeys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(groupKeys);
        var found = new Dictionary<string, SurfaceDecision>(StringComparer.Ordinal);
        var readable = await database.Readable(cancellationToken).ConfigureAwait(false);
        if (readable.IsError || readable.Value is not { } sessions)
        {
            return readable.IsError ? readable.Error : Results.Success<IReadOnlyDictionary<string, SurfaceDecision>>(found);
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
                return tables.IsError ? ContextDatabase.Translate(tables.Error) : Results.Success<IReadOnlyDictionary<string, SurfaceDecision>>(found);
            }

            var looked = await Lookup(read.Value, groupKeys, found, cancellationToken).ConfigureAwait(false);
            return looked.IsError ? looked.Error : found;
        }
    }

    private static async ValueTask<Result<Unit>> Lookup(ISqliteSession session, IReadOnlyCollection<string> groupKeys, Dictionary<string, SurfaceDecision> found, CancellationToken cancellationToken)
    {
        foreach (var key in groupKeys.Distinct(StringComparer.Ordinal))
        {
            var rows = await SqliteQueries.List(session, new SqlStatement(Select, c => c.Bind("$k", key)), DecisionMapper.Instance, cancellationToken).ConfigureAwait(false);
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

    public async ValueTask<Result<Unit>> Save(IReadOnlyList<SurfaceDecision> decisions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        if (decisions.Count == 0)
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
            foreach (var decision in decisions)
            {
                var written = await SqliteQueries.Execute(begun.Value, new SqlStatement(Upsert, c => Bind(c, decision)), cancellationToken).ConfigureAwait(false);
                if (written.IsError)
                {
                    return ContextDatabase.Translate(written.Error);
                }
            }

            var committed = await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            return committed.IsError ? ContextDatabase.Translate(committed.Error) : Unit.Value;
        }
    }

    private static void Bind(SqliteCommand command, SurfaceDecision decision)
    {
        var candidates = Encoding.UTF8.GetString(AwJson.Serialize(decision.CandidateKeys.ToArray(), _json));
        _ = command.Bind("$k", decision.GroupKey).Bind("$choice", ChoiceText(decision.Choice)).Bind("$survivor", decision.SurvivorKey).Bind("$name", decision.MergedName)
            .Bind("$candidates", candidates).Bind("$model", decision.Model).Bind("$decided", GraphText.Time(decision.DecidedAt));
    }

    private static string ChoiceText(SurfaceChoice choice) => choice switch
    {
        SurfaceChoice.Merge => "merge",
        SurfaceChoice.Reject => "reject",
        SurfaceChoice.Separate => "separate",
        _ => throw new ArgumentOutOfRangeException(nameof(choice), choice, "not a surface choice"),
    };

    private static SurfaceChoice ChoiceOf(string text) => text switch
    {
        "merge" => SurfaceChoice.Merge,
        "reject" => SurfaceChoice.Reject,
        "separate" => SurfaceChoice.Separate,
        _ => throw new InvalidOperationException($"surface_pick.choice '{text}' isn't separate, merge or reject."),
    };

    private sealed class CountMapper : IRowMapper<long>
    {
        internal static CountMapper Instance { get; } = new();

        public long Map(SqliteDataReader reader) => reader.GetInt64(0);
    }

    private sealed class DecisionMapper : IRowMapper<SurfaceDecision>
    {
        internal static DecisionMapper Instance { get; } = new();

        public SurfaceDecision Map(SqliteDataReader reader)
        {
            var keys = AwJson.Deserialize<string[]>(Encoding.UTF8.GetBytes(reader.GetString(4)), _json);
            if (keys.IsError)
            {
                throw new InvalidOperationException($"surface_pick.candidate_keys isn't a key list: {keys.Error.Message}");
            }

            return new SurfaceDecision(reader.GetString(0), ChoiceOf(reader.GetString(1)), Text(reader, 2), Text(reader, 3), keys.Value, Text(reader, 5), DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
        }

        private static string? Text(SqliteDataReader reader, int i) => reader.IsDBNull(i) ? null : reader.GetString(i);
    }
}
