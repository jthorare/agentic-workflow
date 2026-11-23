using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The owner muted a row (home spec §3.1).</summary>
/// <param name="Id">The row.</param>
/// <param name="Version">The version after the write.</param>
public sealed record HomeRowMuted(HomeRowId Id, long Version);
