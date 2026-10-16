using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Testing;

/// <summary>
/// The views store in memory, for unit tests; passes <see cref="ViewStoreContract"/>. Rows are JSON text through the
/// package's AwJson options, so they round-trip exactly as in the SQLite store; a page commits once, and a failing
/// event's writes are rolled back as a savepoint would.
/// </summary>
/// <param name="options">The package's AwJson options.</param>
public sealed class InMemoryViewStore(JsonSerializerOptions options) : IViewStore, IViewReader
{
    private readonly Lock _gate = new();
    private Dictionary<string, SortedDictionary<string, string>> _tables = InMemoryViewRows.Empty();
    private ProjectorState? _state;

    /// <inheritdoc/>
    public ValueTask<Result<ProjectorState>> Open(int schemaVersion, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var fresh = _state is null;
            _state = fresh
                ? new ProjectorState(0, schemaVersion, ProjectedAt: null, Rebuilding: false, StalledAt: null, StalledType: null, Fresh: true)
                : _state! with { Fresh = false };   // why: not fresh means a state exists
            return ValueTask.FromResult<Result<ProjectorState>>(_state);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<Result<PageApplied>> Apply(IReadOnlyList<LedgerEvent> page, IReadOnlyList<IProjection> projections,
        DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        var (tables, state) = Snapshot();
        foreach (var recorded in page)
        {
            var before = InMemoryViewRows.Copy(tables);
            var failure = await Fold(recorded, projections, new InMemoryViewRows(tables, options), cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                var stalled = state with { StalledAt = recorded.Sequence, StalledType = recorded.Envelope.Type };
                Commit(before, stalled);
                return new PageApplied(stalled, new ViewStall(recorded.Sequence, recorded.Envelope.Type, failure.Code, failure.Message));
            }

            state = state with { Cursor = recorded.Sequence, ProjectedAt = at, StalledAt = null, StalledType = null };
        }

        Commit(tables, state);
        return new PageApplied(state, Stall: null);
    }

    /// <inheritdoc/>
    public ValueTask<Result<ProjectorState>> Reset(IReadOnlyList<string> views, int schemaVersion, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(views);
        lock (_gate)
        {
            foreach (var view in views.Where(_tables.ContainsKey))
            {
                _tables[view].Clear();
            }

            _state = new ProjectorState(0, schemaVersion, at, Rebuilding: true, StalledAt: null, StalledType: null, Fresh: false);
            return ValueTask.FromResult<Result<ProjectorState>>(_state);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> FinishRebuild(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _state = _state is null ? null : _state with { Rebuilding = false };
            return ValueTask.FromResult<Result<Unit>>(Unit.Value);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<TRow?>> Get<TRow>(string view, string key, CancellationToken cancellationToken)
        where TRow : class
    {
        lock (_gate)
        {
            return new InMemoryViewRows(_tables, options).Get<TRow>(view, key, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<TRow>>> List<TRow>(string view, CancellationToken cancellationToken)
        where TRow : class
    {
        lock (_gate)
        {
            return ValueTask.FromResult(new InMemoryViewRows(_tables, options).List<TRow>(view));
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<TRow>>> Range<TRow>(string view, string afterKey, string? beforeKey, int max, CancellationToken cancellationToken)
        where TRow : class
    {
        lock (_gate)
        {
            return ValueTask.FromResult(new InMemoryViewRows(_tables, options).Range<TRow>(view, afterKey, beforeKey, max));
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<TRow>>> Where<TRow>(string view, ViewColumn column, string value, CancellationToken cancellationToken)
        where TRow : class
    {
        lock (_gate)
        {
            return ValueTask.FromResult(new InMemoryViewRows(_tables, options).Matching<TRow>(view, column, value));
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<ProjectorState?>> State(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult<Result<ProjectorState?>>(_state);
        }
    }

    private static async ValueTask<Error?> Fold(LedgerEvent recorded, IReadOnlyList<IProjection> projections, IViewRows rows, CancellationToken cancellationToken)
    {
        foreach (var projection in projections.Where(p => p.Handles(recorded.Envelope.Type)))
        {
            var failure = await FoldOne(projection, recorded, rows, cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }
        }

        return null;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "Mirrors SqliteViewStore's boundary: a throwing fold stalls the projector (SND-VIEW-001), it doesn't crash it.")]
    private static async ValueTask<Error?> FoldOne(IProjection projection, LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        try
        {
            var applied = await projection.Apply(recorded, rows, cancellationToken).ConfigureAwait(false);
            return applied.IsError ? applied.Error : null;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return SindriErrors.ViewsStalled.With($"{e.GetType().Name}: {e.Message}");
        }
    }

    private (Dictionary<string, SortedDictionary<string, string>> Tables, ProjectorState State) Snapshot()
    {
        lock (_gate)
        {
            return (InMemoryViewRows.Copy(_tables), _state ?? throw new InvalidOperationException("Open the views store before applying a page"));
        }
    }

    private void Commit(Dictionary<string, SortedDictionary<string, string>> tables, ProjectorState state)
    {
        lock (_gate)
        {
            _tables = tables;
            _state = state;
        }
    }
}
