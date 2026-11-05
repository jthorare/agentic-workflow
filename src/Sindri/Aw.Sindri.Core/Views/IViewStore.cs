using Aw.Kernel;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Views;

/// <summary>The projector's writer over the views (a projection writer, ARCH-CQRS-002): queries never take it.</summary>
public interface IViewStore
{
    /// <summary>Creates or migrates the views store (0600) and reads its state; Fresh when this call created it.</summary>
    /// <param name="schemaVersion">The view schema version a new store starts at.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The state.</returns>
    public ValueTask<Result<ProjectorState>> Open(int schemaVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Folds <paramref name="page"/> through the projections that handle each event, in one transaction with a
    /// savepoint per event. A failing event stops the page there: the events before it are committed and the stall
    /// is recorded.
    /// </summary>
    /// <param name="page">The events, oldest first.</param>
    /// <param name="projections">Every registered projection.</param>
    /// <param name="at">When the page was applied (the new ProjectedAt).</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new state and the stall, if any.</returns>
    public ValueTask<Result<PageApplied>> Apply(IReadOnlyList<LedgerEvent> page, IReadOnlyList<IProjection> projections,
        DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>Empties <paramref name="views"/>, sets the cursor to 0, clears a stall, marks the store rebuilding and records <paramref name="rebuiltThrough"/>, all in one transaction.</summary>
    /// <param name="views">The view tables to empty.</param>
    /// <param name="schemaVersion">The schema version the replay folds with.</param>
    /// <param name="at">When the rebuild started.</param>
    /// <param name="rebuiltThrough">The newest <c lang="text">views.rebuilt</c> sequence this reset covers (0 when the ledger has none).</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new state.</returns>
    public ValueTask<Result<ProjectorState>> Reset(IReadOnlyList<string> views, int schemaVersion, DateTimeOffset at, long rebuiltThrough,
        CancellationToken cancellationToken);

    /// <summary>Clears the rebuilding mark once the replay reached the head.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success.</returns>
    public ValueTask<Result<Unit>> FinishRebuild(CancellationToken cancellationToken);
}
