using System.Text;
using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core.EngineReads;
using Aw.Sindri.Core.Launch;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Ledger;

/// <summary>
/// <see cref="IMirroredSequences"/> over the ledger, read-only like <see cref="SqliteLedgerReader"/> (API spec §3.8, E-30). A mirrored row's 'temporal_event_id' is the activity id of the
/// RecordEvent that wrote it and its idempotency key is 'workflow:{address}:{run}:{activity}' (optionally ':{content hash}' after a replay), so the rows of one run's activities are
/// those of the instance (the payload's 'instance' member, which the events_instance_temporal index serves) whose key starts with 'workflow:{address}:{run}:' (an exact, case-sensitive match) and whose temporal_event_id is one asked for; the newest row of an activity wins.
/// </summary>
internal sealed class SqliteMirroredSequences(LedgerDatabase database) : IMirroredSequences
{
    public async ValueTask<Result<IReadOnlyDictionary<string, long>>> Of(InstanceAddress address, string run, IReadOnlyCollection<string> activities, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(activities);
        var found = new Dictionary<string, long>(StringComparer.Ordinal);
        if (activities.Count == 0)
        {
            return Results.Success<IReadOnlyDictionary<string, long>>(found);
        }

        var schema = await database.Schema(cancellationToken).ConfigureAwait(false);
        if (schema.IsError || schema.Value.Applied == 0)
        {
            return schema.IsError ? schema.Error : Results.Success<IReadOnlyDictionary<string, long>>(found);   // a missing or unmigrated ledger mirrors nothing
        }

        var read = await database.Sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return LedgerDatabase.Translate(read.Error);
        }

        await using (read.Value.ConfigureAwait(false))
        {
            var rows = await SqliteQueries.List(read.Value, Statement(address, run, [.. activities]), Pairs.Instance, cancellationToken).ConfigureAwait(false);
            if (rows.IsError)
            {
                return LedgerDatabase.Translate(rows.Error);
            }

            foreach (var (activity, sequence) in rows.Value)
            {
                found[activity] = sequence;
            }

            return Results.Success<IReadOnlyDictionary<string, long>>(found);
        }
    }

    internal static SqlStatement Statement(InstanceAddress address, string run, IReadOnlyList<string> activities)
    {
        var names = Enumerable.Range(0, activities.Count).Select(static i => "$a" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();
        var sql = new StringBuilder("SELECT temporal_event_id, MAX(seq) FROM events WHERE json_extract(payload_json, '$.instance') = $address AND temporal_event_id IN (")
            .AppendJoin(", ", names)
            .Append(") AND substr(idempotency_key, 1, length($prefix)) = $prefix GROUP BY temporal_event_id")
            .ToString();
        var prefix = $"workflow:{address.Value}:{run}:";
        return new SqlStatement(sql, command =>
        {
            _ = command.Bind("$address", address.Value);
            _ = command.Bind("$prefix", prefix);
            for (var i = 0; i < activities.Count; i++)
            {
                _ = command.Bind(names[i], activities[i]);
            }
        });
    }

    private sealed class Pairs : IRowMapper<KeyValuePair<string, long>>
    {
        public static readonly Pairs Instance = new();

        public KeyValuePair<string, long> Map(SqliteDataReader reader) => KeyValuePair.Create(reader.GetString(0), reader.GetInt64(1));
    }
}
