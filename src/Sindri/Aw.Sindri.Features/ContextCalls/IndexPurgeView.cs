namespace Aw.Sindri.Features.ContextCalls;

/// <summary>What a purge request found, or deleted.</summary>
/// <param name="Source">The source id.</param>
/// <param name="Records">Its records, tombstones included.</param>
/// <param name="Chunks">The chunks of those records.</param>
/// <param name="Vectors">The chunk vectors of those chunks.</param>
/// <param name="Purged">True when the rows were deleted; false for a preview.</param>
public sealed record IndexPurgeView(string Source, int Records, int Chunks, int Vectors, bool Purged);
