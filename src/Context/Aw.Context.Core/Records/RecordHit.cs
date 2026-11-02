namespace Aw.Context.Core.Records;

/// <summary>One chunk a search leg found in the record store.</summary>
/// <param name="Chunk">The chunk's key; its 'Record' is the record it belongs to.</param>
/// <param name="Title">The record's (already scrubbed) title.</param>
/// <param name="LineStart">The chunk's first line, or null when the source has no lines.</param>
/// <param name="LineEnd">The chunk's last line, or null.</param>
/// <param name="Score">How well it matched, higher is better (the leg's own scale: it is only compared inside the leg).</param>
public sealed record RecordHit(ChunkKey Chunk, string Title, int? LineStart, int? LineEnd, double Score);
