namespace Aw.JevWire;

/// <summary>POST /v1/systemone's response to a batch: an answer per question key, and the usage.</summary>
/// <param name="Answers">The answers by key.</param>
/// <param name="Usage">The cost.</param>
public sealed record JevBatchResponse(IReadOnlyDictionary<string, JevBatchAnswer>? Answers = null, JevUsage? Usage = null);
