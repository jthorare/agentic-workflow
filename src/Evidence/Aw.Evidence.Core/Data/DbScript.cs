using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Data;

/// <summary>A planned database script (schema 'evidence.db.run/1'): queries against one database, each with what its rows must hold.</summary>
/// <param name="Schema">'evidence.db.run/1' when given.</param>
/// <param name="Connection">The name of a Postgres entry in the targets config; not together with <paramref name="Sqlite"/>.</param>
/// <param name="Sqlite">A SQLite file below the repo; not together with <paramref name="Connection"/>.</param>
/// <param name="Seed">A .sql file run against the run's copy of a SQLite file before the queries; never with a connection.</param>
/// <param name="RowLimit">Rows kept per query, 1 to 1000; absent is <see cref="DefaultRowLimit"/>.</param>
/// <param name="Queries">The queries, in order; at least one.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DbScript(
    string? Schema = null,
    string? Connection = null,
    string? Sqlite = null,
    string? Seed = null,
    int? RowLimit = null,
    IReadOnlyList<DbQuery>? Queries = null)
{
    /// <summary>The schema name scripts may state.</summary>
    public const string SchemaName = "evidence.db.run/1";

    /// <summary>Rows kept per query when the script gives no limit.</summary>
    public const int DefaultRowLimit = 200;

    /// <summary>The most rows a script may ask to keep per query.</summary>
    public const int MaxRowLimit = 1000;

    /// <summary>The queries (empty when none).</summary>
    [JsonIgnore]
    public IReadOnlyList<DbQuery> QueryList => Queries ?? [];

    /// <summary>The rows kept per query.</summary>
    [JsonIgnore]
    public int EffectiveRowLimit => RowLimit ?? DefaultRowLimit;
}
