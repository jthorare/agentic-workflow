using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The owner saw a row (home spec §4.3).</summary>
/// <param name="Id">The row.</param>
/// <param name="Version">The version the owner saw.</param>
public sealed record HomeRowSeen(HomeRowId Id, long Version);
