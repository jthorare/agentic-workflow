namespace Aw.Context.Core.ConceptSynthesis;

/// <summary>One record's vector, the mean of its chunks' vectors.</summary>
/// <param name="Record">The record key.</param>
/// <param name="Version">The record version the vector was built from.</param>
/// <param name="Source">The source the record came from (GitHub, Linear, Slack, docs).</param>
/// <param name="Vector">The L2-normalised vector.</param>
public sealed record RecordVector(string Record, string Version, string Source, ReadOnlyMemory<float> Vector);
