namespace Aw.Sindri.Core.Scheduling;

/// <summary>What one checkpoint did to one store.</summary>
/// <param name="Store">The file name, such as ledger.db.</param>
/// <param name="Truncated">True when the log file was reset to empty; false when a reader or writer held it.</param>
/// <param name="LogBytes">The size of the -wal file before the checkpoint.</param>
public sealed record StoreCheckpoint(string Store, bool Truncated, long LogBytes);
