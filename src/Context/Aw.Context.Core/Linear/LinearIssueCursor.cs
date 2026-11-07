using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Linear;

/// <summary>Where the next page of a list starts: lists run newest first by Linear's update time, then by key, so a page continues after the last issue of the one before.</summary>
/// <param name="UpdatedAt">The last issue's update time.</param>
/// <param name="Key">The last issue's key.</param>
public sealed record LinearIssueCursor(DateTimeOffset UpdatedAt, RecordKey Key);
