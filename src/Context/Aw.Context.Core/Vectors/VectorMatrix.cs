namespace Aw.Context.Core.Vectors;

/// <summary>The in-memory copy of stored vectors a scan runs over: row 'i' is 'Data[i*Dim .. (i+1)*Dim]' and belongs to 'Keys[i]'.</summary>
/// <param name="Dim">The dimension of every row.</param>
/// <param name="Data">The rows, back to back, each L2-normalised.</param>
/// <param name="Keys">The key of each row (a chunk key).</param>
public sealed record VectorMatrix(int Dim, ReadOnlyMemory<float> Data, IReadOnlyList<string> Keys);
