namespace Aw.Embeddings;

/// <summary>One row of a scan.</summary>
/// <param name="Row">The row's index in the matrix.</param>
/// <param name="Score">Its dot product with the query (the cosine, for normalised vectors).</param>
public sealed record ScanHit(int Row, float Score);
