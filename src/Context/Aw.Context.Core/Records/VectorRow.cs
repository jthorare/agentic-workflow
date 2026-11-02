namespace Aw.Context.Core.Records;

/// <summary>A stored vector and its place in the store's order.</summary>
/// <param name="Seq">Monotonic: a cache loads only rows after the last one it has.</param>
/// <param name="Vector">The vector.</param>
public sealed record VectorRow(long Seq, VectorWrite Vector);
