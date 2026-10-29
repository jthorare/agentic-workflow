namespace Aw.Context.Core.Results;

/// <summary>How many nearest-neighbour pairs fell in one cosine bucket.</summary>
/// <param name="Lower">The bucket's lower bound.</param>
/// <param name="Count">The pairs in it.</param>
public sealed record CosineBucket(float Lower, int Count);
