using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Infrastructure.Views;

/// <summary>
/// The projector's writer over views.db (API spec §2.3): one transaction per page and a savepoint per event, so a
/// failing event (an Error or a throw) is rolled back alone, the events before it commit and the stall is recorded.
/// </summary>
internal sealed class SqliteViewStore(ViewsDatabase database, JsonSerializerOptions options) : IViewStore
{
    private const string Savepoint = "projector_event";

    public ValueTask<Result<ProjectorState>> Open(int schemaVersion, CancellationToken cancellationToken) =>
        InSession<ProjectorState>(async session =>
        {
            var inserted = await SqliteQueries.Execute(session, new SqlStatement(ViewSql.InsertState, c => c.Bind("$schemaVersion", schemaVersion)),
                cancellationToken).ConfigureAwait(false);
            if (inserted.IsError)
            {
                return ViewsDatabase.Translate(inserted.Error);
            }

            var state = await ReadState(session, cancellationToken).ConfigureAwait(false);
            return state.IsError ? state.Error : state.Value with { Fresh = inserted.Value == 1 };
        }, cancellationToken);

    public ValueTask<Result<PageApplied>> Apply(IReadOnlyList<LedgerEvent> page, IReadOnlyList<IProjection> projections, DateTimeOffset at,
        CancellationToken cancellationToken) =>
        InSession<PageApplied>(async session =>
        {
            var read = await ReadState(session, cancellationToken).ConfigureAwait(false);
            if (read.IsError)
            {
                return read.Error;
            }

            var state = read.Value;
            var unreadable = new UnreadableRows();
            foreach (var recorded in page)
            {
                var failure = await FoldOne(session, recorded, projections, unreadable, cancellationToken).ConfigureAwait(false);
                if (failure is not null)
                {
                    var stalled = state with { StalledAt = recorded.Sequence, StalledType = recorded.Envelope.Type };
                    var kept = await WriteState(session, stalled, cancellationToken).ConfigureAwait(false);
                    return kept.IsError ? kept.Error : new PageApplied(stalled, new ViewStall(recorded.Sequence, recorded.Envelope.Type, failure.Code, failure.Message), unreadable.Count);
                }

                state = state with { Cursor = recorded.Sequence, ProjectedAt = at, StalledAt = null, StalledType = null };
            }

            var written = await WriteState(session, state, cancellationToken).ConfigureAwait(false);
            return written.IsError ? written.Error : new PageApplied(state, Stall: null, unreadable.Count);
        }, cancellationToken);

    public ValueTask<Result<ProjectorState>> Reset(IReadOnlyList<string> views, int schemaVersion, DateTimeOffset at, long rebuiltThrough,
        CancellationToken cancellationToken) =>
        InSession(async session =>
        {
            foreach (var table in views.Select(ViewSql.Table).OfType<ViewTable>())
            {
                var cleared = await SqliteQueries.Execute(session, new SqlStatement(table.Clear), cancellationToken).ConfigureAwait(false);
                if (cleared.IsError)
                {
                    return ViewsDatabase.Translate(cleared.Error);
                }
            }

            var reset = await SqliteQueries.Execute(session, new SqlStatement(ViewSql.ResetState,
                c => c.Bind("$schemaVersion", schemaVersion).Bind("$at", at).Bind("$rebuiltThrough", rebuiltThrough)), cancellationToken).ConfigureAwait(false);
            return reset.IsError ? ViewsDatabase.Translate(reset.Error) : await ReadState(session, cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

    public ValueTask<Result<Unit>> AdoptRebuiltThrough(long rebuiltThrough, CancellationToken cancellationToken) =>
        InSession(async session =>
        {
            var adopted = await SqliteQueries.Execute(session, new SqlStatement(ViewSql.AdoptRebuiltThrough, c => c.Bind("$rebuiltThrough", rebuiltThrough)),
                cancellationToken).ConfigureAwait(false);
            return adopted.IsError ? ViewsDatabase.Translate(adopted.Error) : Results.Success(Unit.Value);
        }, cancellationToken);

    public ValueTask<Result<Unit>> FinishRebuild(CancellationToken cancellationToken) =>
        InSession(async session =>
        {
            var done = await SqliteQueries.Execute(session, new SqlStatement(ViewSql.FinishRebuild), cancellationToken).ConfigureAwait(false);
            return done.IsError ? ViewsDatabase.Translate(done.Error) : Results.Success(Unit.Value);
        }, cancellationToken);

    /// <summary>Folds one event under a savepoint; returns the failure (an Error or a throw) after rolling the event back, else null.</summary>
    private async ValueTask<Error?> FoldOne(SqliteUnitOfWork session, LedgerEvent recorded, IReadOnlyList<IProjection> projections, UnreadableRows unreadable,
        CancellationToken cancellationToken)
    {
        await session.Transaction.SaveAsync(Savepoint, cancellationToken).ConfigureAwait(false);
        var rows = new SqliteViewRows(session, recorded.Sequence, options, unreadable);
        Error? failure = null;
        foreach (var projection in projections.Where(p => p.Handles(recorded.Envelope.Type)))
        {
            try
            {
                var applied = await projection.Apply(recorded, rows, cancellationToken).ConfigureAwait(false);
                failure = applied.IsError ? applied.Error : null;
            }
            catch (Exception e) when (e is not OperationCanceledException)   // boundary: a throwing fold stalls the projector, it doesn't crash it
            {
                failure = SindriErrors.ViewsStalled.With($"{e.GetType().Name}: {e.Message}");
            }

            if (failure is not null)
            {
                await session.Transaction.RollbackAsync(Savepoint, cancellationToken).ConfigureAwait(false);
                return failure;
            }
        }

        await session.Transaction.ReleaseAsync(Savepoint, cancellationToken).ConfigureAwait(false);
        return null;
    }

    private async ValueTask<Result<T>> InSession<T>(Func<SqliteUnitOfWork, ValueTask<Result<T>>> work, CancellationToken cancellationToken)
    {
        var writable = await database.Writable(cancellationToken).ConfigureAwait(false);
        if (writable.IsError)
        {
            return writable.Error;
        }

        var begun = await writable.Value.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return ViewsDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var result = await work(begun.Value).ConfigureAwait(false);
            if (result.IsError)
            {
                return result;
            }

            var committed = await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            return committed.IsError ? ViewsDatabase.Translate(committed.Error) : result;
        }
    }

    private static async ValueTask<Result<ProjectorState>> ReadState(ISqliteSession session, CancellationToken cancellationToken)
    {
        var state = await SqliteQueries.One(session, new SqlStatement(ViewSql.SelectState), ProjectorStateMapper.Instance, cancellationToken).ConfigureAwait(false);
        return state.IsError ? ViewsDatabase.Translate(state.Error) : state;
    }

    private static async ValueTask<Result<Unit>> WriteState(ISqliteSession session, ProjectorState state, CancellationToken cancellationToken)
    {
        var projectedAt = state.ProjectedAt?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        var written = await SqliteQueries.Execute(session, new SqlStatement(ViewSql.UpdateState, c => c
            .Bind("$cursor", state.Cursor).Bind("$projectedAt", projectedAt).Bind("$stalledAt", state.StalledAt).Bind("$stalledType", state.StalledType)),
            cancellationToken).ConfigureAwait(false);
        return written.IsError ? ViewsDatabase.Translate(written.Error) : Unit.Value;
    }
}
