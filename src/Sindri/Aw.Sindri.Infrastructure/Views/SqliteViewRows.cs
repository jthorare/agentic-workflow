using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Infrastructure.Views;

/// <summary>View rows over an open views.db session: JSON text through the package's AwJson options, each write tagged with the event's sequence.</summary>
/// <param name="session">The page transaction (or a read session, for reads).</param>
/// <param name="sequence">The sequence of the event being folded (0 for reads).</param>
/// <param name="options">The package's AwJson options.</param>
/// <param name="unreadable">When given, a stored row that can't be deserialized (written under an older shape) reads as absent and is counted here; when null it is an error.</param>
/// <param name="savepoint">When given, opened before the first write (the projector's per-event savepoint).</param>
internal sealed class SqliteViewRows(ISqliteSession session, long sequence, JsonSerializerOptions options, UnreadableRows? unreadable = null,
    EventSavepoint? savepoint = null) : IViewRows
{
    public async ValueTask<Result<TRow?>> Get<TRow>(string view, string key, CancellationToken cancellationToken)
        where TRow : class
    {
        if (ViewSql.Table(view) is not { } table)
        {
            return Unknown(view);
        }

        var rows = await SqliteQueries.List(session, new SqlStatement(table.Select, c => c.Bind("$key", key)), RowJsonMapper.Instance, cancellationToken)
            .ConfigureAwait(false);
        if (rows.IsError || rows.Value.Count == 0)
        {
            return rows.IsError ? ViewsDatabase.Translate(rows.Error) : Results.Success<TRow?>(value: null);
        }

        var row = AwJson.Deserialize<TRow>(Encoding.UTF8.GetBytes(rows.Value[0]), options);
        if (row.IsError && unreadable is not null)
        {
            unreadable.Note();
            return Results.Success<TRow?>(value: null);
        }

        return row.IsError ? row.Error : Found(row.Value);
    }

    /// <summary>Every row of <paramref name="view"/>, ordered by key.</summary>
    /// <typeparam name="TRow">The row record.</typeparam>
    /// <param name="view">The view.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The rows.</returns>
    public async ValueTask<Result<IReadOnlyList<TRow>>> List<TRow>(string view, CancellationToken cancellationToken)
        where TRow : class
    {
        if (ViewSql.Table(view) is not { } table)
        {
            return Unknown(view);
        }

        var texts = await SqliteQueries.List(session, new SqlStatement(table.SelectAll), RowJsonMapper.Instance, cancellationToken).ConfigureAwait(false);
        return Decode<TRow>(texts);
    }

    /// <summary>The rows of <paramref name="view"/> with keys strictly between the bounds, ordered by key, at most <paramref name="max"/>.</summary>
    /// <typeparam name="TRow">The row record.</typeparam>
    /// <param name="view">The view.</param>
    /// <param name="afterKey">The lower bound (exclusive).</param>
    /// <param name="beforeKey">The upper bound (exclusive), or null for none.</param>
    /// <param name="max">The most rows to return.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The rows.</returns>
    public async ValueTask<Result<IReadOnlyList<TRow>>> Range<TRow>(string view, string afterKey, string? beforeKey, int max, CancellationToken cancellationToken)
        where TRow : class
    {
        if (ViewSql.Table(view) is not { } table)
        {
            return Unknown(view);
        }

        var statement = beforeKey is null
            ? new SqlStatement(table.SelectAfter, c => c.Bind("$after", afterKey).Bind("$max", max))
            : new SqlStatement(table.SelectBetween, c => c.Bind("$after", afterKey).Bind("$before", beforeKey).Bind("$max", max));
        var texts = await SqliteQueries.List(session, statement, RowJsonMapper.Instance, cancellationToken).ConfigureAwait(false);
        return Decode<TRow>(texts);
    }

    public async ValueTask<Result<IReadOnlyList<TRow>>> Where<TRow>(string view, ViewColumn column, string value, CancellationToken cancellationToken)
        where TRow : class
    {
        if (ViewSql.Table(view) is not { } table)
        {
            return Unknown(view);
        }

        if (table.SelectWhere(column) is not { } sql)
        {
            return SindriErrors.InvalidInput.With($"The '{view}' view doesn't index {column}");
        }

        var texts = await SqliteQueries.List(session, new SqlStatement(sql, c => c.Bind("$value", value)), RowJsonMapper.Instance, cancellationToken).ConfigureAwait(false);
        return Decode<TRow>(texts);
    }

    private Result<IReadOnlyList<TRow>> Decode<TRow>(Result<IReadOnlyList<string>> texts)
        where TRow : class
    {
        if (texts.IsError)
        {
            return ViewsDatabase.Translate(texts.Error);
        }

        var rows = new List<TRow>(texts.Value.Count);
        foreach (var text in texts.Value)
        {
            var row = AwJson.Deserialize<TRow>(Encoding.UTF8.GetBytes(text), options);
            if (row.IsError && unreadable is not null)
            {
                unreadable.Note();
                continue;
            }

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
        var text = Encoding.UTF8.GetString(AwJson.Serialize(row, options));
        return Write(view, t => new SqlStatement(t.Upsert, c => c.Bind("$key", key).Bind("$row", text).Bind("$seq", sequence)), cancellationToken);
    }

    public ValueTask<Result<Unit>> Delete(string view, string key, CancellationToken cancellationToken) =>
        Write(view, t => new SqlStatement(t.Delete, c => c.Bind("$key", key)), cancellationToken);

    public ValueTask<Result<Unit>> Clear(string view, CancellationToken cancellationToken) =>
        Write(view, t => new SqlStatement(t.Clear), cancellationToken);

    private async ValueTask<Result<Unit>> Write(string view, Func<ViewTable, SqlStatement> statement, CancellationToken cancellationToken)
    {
        if (ViewSql.Table(view) is not { } table)
        {
            return Unknown(view);
        }

        if (savepoint is not null)
        {
            await savepoint.Open(cancellationToken).ConfigureAwait(false);
        }

        var written = await SqliteQueries.Execute(session, statement(table), cancellationToken).ConfigureAwait(false);
        return written.IsError ? ViewsDatabase.Translate(written.Error) : Unit.Value;
    }

    private static Result<TRow?> Found<TRow>(TRow? row)
        where TRow : class => row;

    private static Error Unknown(string view) => SindriErrors.InvalidInput.With($"'{view}' isn't a view");
}
