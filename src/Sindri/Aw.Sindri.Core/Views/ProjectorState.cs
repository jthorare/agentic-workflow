namespace Aw.Sindri.Core.Views;

/// <summary>The views store's projector_state row (API spec §2.3).</summary>
/// <param name="Cursor">The last ledger sequence applied.</param>
/// <param name="SchemaVersion">The view schema version the rows were folded with.</param>
/// <param name="ProjectedAt">When the cursor last moved, or null.</param>
/// <param name="Rebuilding">True between a reset and its FinishRebuild.</param>
/// <param name="StalledAt">The sequence the projector stalled at, or null.</param>
/// <param name="StalledType">That event's type, or null.</param>
/// <param name="Fresh">True when this Open created the store (views.db was missing).</param>
/// <param name="RebuiltThrough">The newest <c lang="text">views.rebuilt</c> sequence a reset has covered, or null for a store from before the column (it rebuilds once, which sets it) (#2054).</param>
public sealed record ProjectorState(long Cursor, int SchemaVersion, DateTimeOffset? ProjectedAt, bool Rebuilding,
    long? StalledAt, string? StalledType, bool Fresh, long? RebuiltThrough = null);
