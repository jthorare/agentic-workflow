using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Infrastructure.Views;

/// <summary>One view's table and its statements; <paramref name="Name"/> comes only from <see cref="ViewSql"/>'s fixed map.</summary>
/// <param name="Name">The table name.</param>
/// <param name="Columns">The indexed columns a read can filter by, by <see cref="ViewColumn"/>; empty when the view has none.</param>
internal sealed record ViewTable(string Name, IReadOnlyDictionary<ViewColumn, string>? Columns = null)
{
    /// <summary>The statement that selects the rows whose <paramref name="column"/> equals <c lang="sql">$value</c>, or null when the table doesn't index it.</summary>
    /// <param name="column">The column.</param>
    /// <returns>The SQL, built only from this table's fixed column names.</returns>
    public string? SelectWhere(ViewColumn column) =>
        Columns is not null && Columns.TryGetValue(column, out var name) ? $"SELECT row_json FROM {Name} WHERE {name} = $value ORDER BY key" : null;

    public string Select => $"SELECT row_json FROM {Name} WHERE key = $key";

    public string SelectAll => $"SELECT row_json FROM {Name} ORDER BY key";

    /// <summary>Keys after <c lang="sql">$after</c> (exclusive), in order, at most <c lang="sql">$max</c>.</summary>
    public string SelectAfter => $"SELECT row_json FROM {Name} WHERE key > $after ORDER BY key LIMIT $max";

    /// <summary>Keys between <c lang="sql">$after</c> and <c lang="sql">$before</c> (both exclusive), in order, at most <c lang="sql">$max</c>.</summary>
    public string SelectBetween => $"SELECT row_json FROM {Name} WHERE key > $after AND key < $before ORDER BY key LIMIT $max";

    public string Upsert =>
        $"INSERT INTO {Name} (key, row_json, seq) VALUES ($key, $row, $seq) ON CONFLICT (key) DO UPDATE SET row_json = excluded.row_json, seq = excluded.seq";

    public string Delete => $"DELETE FROM {Name} WHERE key = $key";

    public string Clear => $"DELETE FROM {Name}";
}
