using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>A snoozed row woke: its time came or addressed activity arrived (home spec §5.3).</summary>
/// <param name="Id">The row.</param>
/// <param name="Version">The version after the write.</param>
public sealed record HomeRowWoke(HomeRowId Id, long Version);
