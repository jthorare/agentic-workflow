namespace Aw.Context.Core.Records;

/// <summary>A stored chunk that has no vector yet for a model (spec §4.2, §5.6).</summary>
/// <param name="Key">The chunk key.</param>
/// <param name="Text">The chunk's scrubbed text: what gets embedded.</param>
/// <param name="TextHash">The chunk's hash: the vector is stored only while the chunk still has it.</param>
public sealed record ChunkToEmbed(string Key, string Text, string TextHash);
