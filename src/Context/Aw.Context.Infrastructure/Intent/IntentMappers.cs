using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Graph;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Context.Infrastructure.Intent;

/// <summary>The intent rows' mappers, one per row shape.</summary>
internal static class IntentMappers
{
    private static readonly string[] _verdicts = ["contradicts", "agrees", "supersedes", "unread", "refused"];
    private static readonly string[] _rules = ["issue-key", "url", "path", "identifier", "quoted-title", "name", "alias"];

    internal static IRowMapper<IntentRecord> Record { get; } = new Mapper<IntentRecord>(r =>
        new(RecordKey.From(r.GetString(0)), SourceId.From(r.GetString(1)), r.GetString(2), r.GetString(3), Split(r.GetString(4))) { CreatedAt = r.IsDBNull(5) ? null : SqliteRead.GetTimestamp(r, 5), FetchedAt = SqliteRead.GetTimestamp(r, 6) });

    internal static IRowMapper<IntentLink> Link { get; } = new Mapper<IntentLink>(r =>
        new(RecordKey.From(r.GetString(0)), NodeKey.From(r.GetString(1)), RuleOf(r.GetString(2)), r.GetDouble(3), r.GetString(4)));

    internal static IRowMapper<DecisionRow> Decision { get; } = new Mapper<DecisionRow>(r =>
        new(RecordKey.From(r.GetString(0)), r.GetString(1), r.GetString(2), GraphText.Provenance(r.GetString(3)), SqliteRead.GetTimestamp(r, 4)));

    internal static IRowMapper<GraphEdge> Edge { get; } = new Mapper<GraphEdge>(r =>
        new(NodeKey.From(r.GetString(0)), NodeKey.From(r.GetString(1)), GraphText.EdgeType(r.GetString(2)), GraphText.Provenance(r.GetString(3)), r.GetDouble(4), At: SqliteRead.GetTimestamp(r, 5)));

    internal static IRowMapper<RecordKey> Key { get; } = new Mapper<RecordKey>(r => RecordKey.From(r.GetString(0)));

    internal static IRowMapper<DecisionConflict> Conflict { get; } = new Mapper<DecisionConflict>(r =>
        new(NodeKey.From(r.GetString(0)), NodeKey.From(r.GetString(1)), VerdictOf(r.GetString(2)), SqliteRead.GetTimestamp(r, 3)) { Tries = r.GetInt32(4) });

    internal static IRowMapper<IntentSourceState> State { get; } = new Mapper<IntentSourceState>(r =>
        new(r.IsDBNull(0) ? null : r.GetString(0), r.IsDBNull(1) ? null : SqliteRead.GetTimestamp(r, 1), r.IsDBNull(2) ? null : r.GetString(2),
            r.GetInt32(3), r.GetInt32(4), r.GetInt32(5), r.GetInt32(6)));

    /// <summary>Binds a record's creation instant as UTC round-trip text, or NULL when the source gave none.</summary>
    internal static SqliteCommand BindCreated(this SqliteCommand command, DateTimeOffset? at) =>
        at is { } value ? command.Bind("@created", value) : command.Bind("@created", (string?)null);

    internal static string Of(ConflictVerdict verdict) => _verdicts[(int)verdict];

    internal static string Joined(IReadOnlyList<string> aliases) => string.Join('\n', aliases);

    internal static string Of(LinkRule rule) => _rules[(int)rule];

    private static ConflictVerdict VerdictOf(string text) =>
        (ConflictVerdict)(Array.IndexOf(_verdicts, text) is var i and >= 0 ? i : throw new InvalidOperationException($"'{text}' isn't a stored verdict."));

    private static LinkRule RuleOf(string text) =>
        (LinkRule)(Array.IndexOf(_rules, text) is var i and >= 0 ? i : throw new InvalidOperationException($"'{text}' isn't a stored link rule."));

    private static string[] Split(string aliases) => aliases.Length == 0 ? [] : aliases.Split('\n');

    private sealed class Mapper<T>(Func<SqliteDataReader, T> map) : IRowMapper<T>
    {
        public T Map(SqliteDataReader reader) => map(reader);
    }
}
