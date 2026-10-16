using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Infrastructure.Views;

/// <summary>
/// The queries' read side of views.db (read sessions only: busy_timeout and query_only). It never creates the file: a
/// missing views.db has no state, no rows and no error (doctor and queries before the first start).
/// </summary>
internal sealed class SqliteViewReader(ViewsDatabase database, JsonSerializerOptions options) : IViewReader
{
    public async ValueTask<Result<TRow?>> Get<TRow>(string view, string key, CancellationToken cancellationToken)
        where TRow : class =>
        database.Exists
            ? await Read(view, Results.Success<TRow?>(value: null), rows => rows.Get<TRow>(view, key, cancellationToken), cancellationToken).ConfigureAwait(false)
            : Results.Success<TRow?>(value: null);

    public async ValueTask<Result<IReadOnlyList<TRow>>> List<TRow>(string view, CancellationToken cancellationToken)
        where TRow : class =>
        database.Exists
            ? await Read(view, Results.Success<IReadOnlyList<TRow>>([]), rows => rows.List<TRow>(view, cancellationToken), cancellationToken).ConfigureAwait(false)
            : Results.Success<IReadOnlyList<TRow>>([]);

    public async ValueTask<Result<IReadOnlyList<TRow>>> Range<TRow>(string view, string afterKey, string? beforeKey, int max, CancellationToken cancellationToken)
        where TRow : class =>
        database.Exists
            ? await Read(view, Results.Success<IReadOnlyList<TRow>>([]), rows => rows.Range<TRow>(view, afterKey, beforeKey, max, cancellationToken), cancellationToken).ConfigureAwait(false)
            : Results.Success<IReadOnlyList<TRow>>([]);

    public async ValueTask<Result<IReadOnlyList<TRow>>> Where<TRow>(string view, ViewColumn column, string value, CancellationToken cancellationToken)
        where TRow : class =>
        database.Exists
            ? await Read(view, Results.Success<IReadOnlyList<TRow>>([]), rows => rows.Where<TRow>(view, column, value, cancellationToken), cancellationToken).ConfigureAwait(false)
            : Results.Success<IReadOnlyList<TRow>>([]);

    public async ValueTask<Result<ProjectorState?>> State(CancellationToken cancellationToken)
    {
        if (!database.Exists)
        {
            return Results.Success<ProjectorState?>(value: null);
        }

        var read = await database.Sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return ViewsDatabase.Translate(read.Error);
        }

        await using (read.Value.ConfigureAwait(false))
        {
            var present = await HasTable(read.Value, ViewSql.StateTable, cancellationToken).ConfigureAwait(false);
            if (present.IsError || !present.Value)
            {
                return present.IsError ? ViewsDatabase.Translate(present.Error) : Results.Success<ProjectorState?>(value: null);
            }

            var state = await SqliteQueries.List(read.Value, new SqlStatement(ViewSql.SelectState), ProjectorStateMapper.Instance, cancellationToken)
                .ConfigureAwait(false);
            return state.IsError ? ViewsDatabase.Translate(state.Error) : Results.Success(state.Value.Count == 0 ? null : state.Value[0]);
        }
    }

    private static async ValueTask<Result<bool>> HasTable(ISqliteSession session, string name, CancellationToken cancellationToken) =>
        await SqliteQueries.Exists(session, new SqlStatement(ViewSql.TableExists, c => c.Bind("$name", name)), cancellationToken).ConfigureAwait(false);

    /// <summary>Runs <paramref name="query"/>; <paramref name="empty"/> when the file exists but the view's table doesn't yet (the projector's migration hasn't committed).</summary>
    private async ValueTask<Result<T>> Read<T>(string view, Result<T> empty, Func<SqliteViewRows, ValueTask<Result<T>>> query, CancellationToken cancellationToken)
    {
        var read = await database.Sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return ViewsDatabase.Translate(read.Error);
        }

        await using (read.Value.ConfigureAwait(false))
        {
            if (ViewSql.Table(view) is { } table)
            {
                var present = await HasTable(read.Value, table.Name, cancellationToken).ConfigureAwait(false);
                if (present.IsError || !present.Value)
                {
                    return present.IsError ? ViewsDatabase.Translate(present.Error) : empty;
                }
            }

            return await query(new SqliteViewRows(read.Value, 0, options)).ConfigureAwait(false);
        }
    }
}
