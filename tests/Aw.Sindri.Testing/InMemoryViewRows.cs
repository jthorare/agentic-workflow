using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Testing;

/// <summary>View tables in memory: each row is its JSON text, ordered by key, as the SQLite views store keeps it.</summary>
/// <param name="tables">The tables, by view name.</param>
/// <param name="options">The package's AwJson options.</param>
internal sealed class InMemoryViewRows(Dictionary<string, SortedDictionary<string, string>> tables, JsonSerializerOptions options) : IViewRows
{
    /// <summary>Empty tables for every view.</summary>
    /// <returns>The tables.</returns>
    public static Dictionary<string, SortedDictionary<string, string>> Empty() =>
        ViewNames.All.ToDictionary(v => v, _ => new SortedDictionary<string, string>(StringComparer.Ordinal), StringComparer.Ordinal);

    /// <summary>A deep copy of <paramref name="source"/>.</summary>
    /// <param name="source">The tables.</param>
    /// <returns>The copy.</returns>
    public static Dictionary<string, SortedDictionary<string, string>> Copy(Dictionary<string, SortedDictionary<string, string>> source) =>
        source.ToDictionary(t => t.Key, t => new SortedDictionary<string, string>(t.Value, StringComparer.Ordinal), StringComparer.Ordinal);

    public ValueTask<Result<TRow?>> Get<TRow>(string view, string key, CancellationToken cancellationToken)
        where TRow : class
    {
        if (!tables.TryGetValue(view, out var table))
        {
            return ValueTask.FromResult<Result<TRow?>>(Unknown(view));
        }

        if (!table.TryGetValue(key, out var text))
        {
            return ValueTask.FromResult<Result<TRow?>>((TRow?)null);
        }

        var row = AwJson.Deserialize<TRow>(Encoding.UTF8.GetBytes(text), options);
        return ValueTask.FromResult(row.IsError ? row.Error : Found(row.Value));
    }

    /// <summary>Every row of <paramref name="view"/>, ordered by key.</summary>
    /// <typeparam name="TRow">The row record.</typeparam>
    /// <param name="view">The view.</param>
    /// <returns>The rows.</returns>
    public Result<IReadOnlyList<TRow>> List<TRow>(string view)
        where TRow : class
        => tables.TryGetValue(view, out var table) ? Read<TRow>(table.Values) : Unknown(view);

    /// <summary>The rows of <paramref name="view"/> with keys strictly between the bounds, in key order, at most <paramref name="max"/>.</summary>
    /// <typeparam name="TRow">The row record.</typeparam>
    /// <param name="view">The view.</param>
    /// <param name="afterKey">The lower bound (exclusive).</param>
    /// <param name="beforeKey">The upper bound (exclusive), or null.</param>
    /// <param name="max">The most rows.</param>
    /// <returns>The rows.</returns>
    public Result<IReadOnlyList<TRow>> Range<TRow>(string view, string afterKey, string? beforeKey, int max)
        where TRow : class
    {
        if (!tables.TryGetValue(view, out var table))
        {
            return Unknown(view);
        }

        var texts = table
            .Where(r => string.CompareOrdinal(r.Key, afterKey) > 0 && (beforeKey is null || string.CompareOrdinal(r.Key, beforeKey) < 0))
            .Take(max)
            .Select(r => r.Value);
        return Read<TRow>(texts);
    }

    public ValueTask<Result<IReadOnlyList<TRow>>> Where<TRow>(string view, ViewColumn column, string value, CancellationToken cancellationToken)
        where TRow : class => ValueTask.FromResult(Matching<TRow>(view, column, value));

    /// <summary>The rows of <paramref name="view"/> whose <paramref name="column"/> equals <paramref name="value"/>, ordered by key.</summary>
    /// <typeparam name="TRow">The row record.</typeparam>
    /// <param name="view">The view.</param>
    /// <param name="column">The column.</param>
    /// <param name="value">The value.</param>
    /// <returns>The rows.</returns>
    public Result<IReadOnlyList<TRow>> Matching<TRow>(string view, ViewColumn column, string value)
        where TRow : class
    {
        if (!tables.TryGetValue(view, out var table))
        {
            return Unknown(view);
        }

        if (ViewColumns.JsonPath(view, column) is not { } path)
        {
            return SindriErrors.InvalidInput.With($"The '{view}' view doesn't index {column}");
        }

        return Read<TRow>(table.Values.Where(text => string.Equals(ValueAt(text, path), value, StringComparison.Ordinal)));
    }

    private static string? ValueAt(string text, IReadOnlyList<string> path)
    {
        using var document = JsonDocument.Parse(text);
        var at = document.RootElement;
        foreach (var name in path)
        {
            if (at.ValueKind != JsonValueKind.Object || !at.TryGetProperty(name, out at))
            {
                return null;
            }
        }

        return at.ValueKind == JsonValueKind.String ? at.GetString() : null;
    }

    private Result<IReadOnlyList<TRow>> Read<TRow>(IEnumerable<string> texts)
        where TRow : class
    {
        var rows = new List<TRow>();
        foreach (var text in texts)
        {
            var row = AwJson.Deserialize<TRow>(Encoding.UTF8.GetBytes(text), options);
            if (row.IsError)
            {
                return row.Error;
            }

            rows.Add(row.Value);
        }

        return rows;
    }

    public ValueTask<Result<Unit>> Put<TRow>(string view, string key, TRow row, CancellationToken cancellationToken)
        where TRow : class
    {
        if (!tables.TryGetValue(view, out var table))
        {
            return ValueTask.FromResult<Result<Unit>>(Unknown(view));
        }

        table[key] = Encoding.UTF8.GetString(AwJson.Serialize(row, options));
        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }

    public ValueTask<Result<Unit>> Delete(string view, string key, CancellationToken cancellationToken)
    {
        if (!tables.TryGetValue(view, out var table))
        {
            return ValueTask.FromResult<Result<Unit>>(Unknown(view));
        }

        _ = table.Remove(key);
        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }

    private static Result<TRow?> Found<TRow>(TRow? row)
        where TRow : class => row;

    private static Error Unknown(string view) => SindriErrors.InvalidInput.With($"'{view}' isn't a view");
}
