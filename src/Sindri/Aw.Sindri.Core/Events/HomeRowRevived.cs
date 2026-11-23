using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>A done row came back because its source needs the owner again (home spec §5.4).</summary>
/// <param name="Id">The row.</param>
/// <param name="Version">The version after the write.</param>
public sealed record HomeRowRevived(HomeRowId Id, long Version);
