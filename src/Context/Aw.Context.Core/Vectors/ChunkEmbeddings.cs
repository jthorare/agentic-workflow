namespace Aw.Context.Core.Vectors;

/// <summary>The vectors of a batch of chunk texts.</summary>
/// <param name="Model">The model name and digest that made them.</param>
/// <param name="Vectors">One vector per text, in the order of the texts; the store L2-normalises on write.</param>
public sealed record ChunkEmbeddings(string Model, IReadOnlyList<ReadOnlyMemory<float>> Vectors);
