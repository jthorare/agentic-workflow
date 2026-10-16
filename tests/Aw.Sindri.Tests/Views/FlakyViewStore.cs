using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Views;

/// <summary>The in-memory views store with calls that can be made to fail: the next <see cref="FailOpens"/> opens, and every read while <see cref="FailReads"/>.</summary>
internal sealed class FlakyViewStore(InMemoryViewStore inner) : IViewStore, IViewReader
{
    public int FailOpens { get; set; }

    public bool FailReads { get; set; }

    /// <summary>The next this many Apply calls fail.</summary>
    public int FailApplies { get; set; }

    public ValueTask<Result<ProjectorState>> Open(int schemaVersion, CancellationToken cancellationToken)
    {
        if (FailOpens > 0)
        {
            FailOpens--;
            return ValueTask.FromResult<Result<ProjectorState>>(SindriErrors.ViewsStalled.With("views.db can't be opened (fixture)"));
        }

        return inner.Open(schemaVersion, cancellationToken);
    }

    public ValueTask<Result<PageApplied>> Apply(IReadOnlyList<LedgerEvent> page, IReadOnlyList<IProjection> projections, DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        if (FailApplies > 0)
        {
            FailApplies--;
            return ValueTask.FromResult<Result<PageApplied>>(SindriErrors.ViewsStalled.With("views.db can't be written (fixture)"));
        }

        return inner.Apply(page, projections, at, cancellationToken);
    }

    public ValueTask<Result<ProjectorState>> Reset(IReadOnlyList<string> views, int schemaVersion, DateTimeOffset at, CancellationToken cancellationToken) =>
        inner.Reset(views, schemaVersion, at, cancellationToken);

    public ValueTask<Result<Unit>> FinishRebuild(CancellationToken cancellationToken) => inner.FinishRebuild(cancellationToken);

    public ValueTask<Result<TRow?>> Get<TRow>(string view, string key, CancellationToken cancellationToken)
        where TRow : class => FailReads ? ValueTask.FromResult<Result<TRow?>>(Unreadable) : inner.Get<TRow>(view, key, cancellationToken);

    public ValueTask<Result<IReadOnlyList<TRow>>> List<TRow>(string view, CancellationToken cancellationToken)
        where TRow : class => FailReads ? ValueTask.FromResult<Result<IReadOnlyList<TRow>>>(Unreadable) : inner.List<TRow>(view, cancellationToken);

    public ValueTask<Result<IReadOnlyList<TRow>>> Range<TRow>(string view, string afterKey, string? beforeKey, int max, CancellationToken cancellationToken)
        where TRow : class => FailReads
            ? ValueTask.FromResult<Result<IReadOnlyList<TRow>>>(Unreadable)
            : inner.Range<TRow>(view, afterKey, beforeKey, max, cancellationToken);

    public ValueTask<Result<IReadOnlyList<TRow>>> Where<TRow>(string view, ViewColumn column, string value, CancellationToken cancellationToken)
        where TRow : class => FailReads
            ? ValueTask.FromResult<Result<IReadOnlyList<TRow>>>(Unreadable)
            : inner.Where<TRow>(view, column, value, cancellationToken);

    public ValueTask<Result<ProjectorState?>> State(CancellationToken cancellationToken) =>
        FailReads ? ValueTask.FromResult<Result<ProjectorState?>>(Unreadable) : inner.State(cancellationToken);

    private static Error Unreadable => SindriErrors.ViewsStalled.With("views.db can't be read (fixture)");
}
