using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>A row's trigger stopped holding, so the row leaves Home (home spec §3.2).</summary>
/// <param name="Id">The row.</param>
public sealed record HomeRowCleared(HomeRowId Id);
