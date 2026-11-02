namespace Aw.Context.Core.Vectors;

/// <summary>One scan hit.</summary>
/// <param name="Row">The row's index in the matrix.</param>
/// <param name="Key">The row's key.</param>
/// <param name="Score">The cosine similarity, 1 for the same direction.</param>
public sealed record VectorHit(int Row, string Key, float Score);
