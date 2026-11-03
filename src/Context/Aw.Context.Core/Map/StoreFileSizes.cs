namespace Aw.Context.Core.Map;

/// <summary>The size of context.db and of its write-ahead log on disk.</summary>
/// <param name="DatabaseBytes">context.db.</param>
/// <param name="WalBytes">context.db-wal (0 when the file doesn't exist).</param>
public sealed record StoreFileSizes(long DatabaseBytes, long WalBytes);
