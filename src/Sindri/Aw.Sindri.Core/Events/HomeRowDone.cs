using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The owner marked a row done (home spec §5.4).</summary>
/// <param name="Id">The row.</param>
/// <param name="Version">The version after the write.</param>
/// <param name="At">When it was marked done, or null for the event's time.</param>
public sealed record HomeRowDone(HomeRowId Id, long Version, DateTimeOffset? At);
