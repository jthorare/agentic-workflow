namespace Aw.Context.Core.Records;

/// <summary>How many live records (not removed, not blocked) the store holds, and how many of them have a chunk vector.</summary>
/// <param name="Live">The live records.</param>
/// <param name="Embedded">Those with at least one chunk vector.</param>
public sealed record LiveRecordCounts(int Live, int Embedded);
