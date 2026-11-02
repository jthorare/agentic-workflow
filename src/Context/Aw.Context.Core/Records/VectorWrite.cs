namespace Aw.Context.Core.Records;

/// <summary>One vector to store (an 'embedding' row, spec §4.2): L2-normalised, so cosine is a dot product.</summary>
/// <param name="Space">What it embeds.</param>
/// <param name="Key">A chunk key, or a graph node key.</param>
/// <param name="Model">The model name and digest from the pin.</param>
/// <param name="TextHash">The hash of what was embedded; a chunk vector is stored only while the chunk still has this hash.</param>
/// <param name="Vec">The components.</param>
public sealed record VectorWrite(VectorSpace Space, string Key, string Model, string TextHash, ReadOnlyMemory<float> Vec);
