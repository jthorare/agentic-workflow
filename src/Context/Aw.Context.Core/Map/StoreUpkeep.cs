namespace Aw.Context.Core.Map;

/// <summary>What one upkeep pass did to context.db: the planner stats and the write-ahead log.</summary>
/// <param name="Optimized">The planner statistics (sqlite_stat1) were refreshed.</param>
/// <param name="Truncated">The log was checkpointed completely and reset to 0 bytes; false when a reader or writer held it.</param>
/// <param name="LogPages">Pages in the log when the checkpoint ran.</param>
/// <param name="CheckpointedPages">Pages the checkpoint moved into the database.</param>
public sealed record StoreUpkeep(bool Optimized, bool Truncated, long LogPages, long CheckpointedPages);
