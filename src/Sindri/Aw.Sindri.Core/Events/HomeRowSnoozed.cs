using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The owner snoozed a row (home spec §5.3).</summary>
/// <param name="Id">The row.</param>
/// <param name="Version">The version after the write.</param>
/// <param name="Until">When the row wakes.</param>
/// <param name="UnlessReplied">True to wake it early when addressed activity arrives.</param>
public sealed record HomeRowSnoozed(HomeRowId Id, long Version, DateTimeOffset Until, bool UnlessReplied);
