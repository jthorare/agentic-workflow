namespace Aw.Context.Core.Records;

/// <summary>What one source holds in the record store, as 'sindri index purge' shows it before it deletes (owned-index spec §5.8).</summary>
/// <param name="Records">The source's records, tombstones included (a purge deletes them too).</param>
/// <param name="Chunks">The chunks of those records.</param>
/// <param name="Vectors">The chunk vectors of those chunks.</param>
public sealed record SourceCounts(int Records, int Chunks, int Vectors);
