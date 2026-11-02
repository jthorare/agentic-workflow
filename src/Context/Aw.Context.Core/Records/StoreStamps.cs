namespace Aw.Context.Core.Records;

/// <summary>The store's two change counters ('store_epoch', spec §4.2): what the in-memory caches watch.</summary>
/// <param name="RecordStamp">Bumped once per committed page and once per committed vector batch.</param>
/// <param name="DeleteEpoch">Bumped by every vector removal, from any cause: a cache that sees it move rebuilds fully.</param>
public sealed record StoreStamps(long RecordStamp, long DeleteEpoch);
