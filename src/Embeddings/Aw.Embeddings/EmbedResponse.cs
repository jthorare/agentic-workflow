namespace Aw.Embeddings;

/// <summary>The answer of POST /api/embed; only the vectors are read.</summary>
/// <param name="Embeddings">One vector per input.</param>
internal sealed record EmbedResponse(float[][]? Embeddings);
