using Aw.Sindri.Core.Home;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>A row's blocks, why chip or state changed at the source (home spec §3.1). A null member leaves the field as it was.</summary>
/// <param name="Id">The row.</param>
/// <param name="Version">The version after the change.</param>
/// <param name="Title">The new title, or null.</param>
/// <param name="Facts">The new facts, or null.</param>
/// <param name="WaitsOn">The new waits-on, or null.</param>
/// <param name="Rank">The new rank, or null.</param>
/// <param name="BlocksList">The new blocks list, or null.</param>
/// <param name="Links">The new cross-links, or null.</param>
/// <param name="Actions">The new action ids, or null.</param>
/// <param name="Source">The new source, or null.</param>
/// <param name="Edges">The row's whole edge set when it changed, or null.</param>
public sealed record HomeRowChanged(HomeRowId Id, long Version, string? Title, IReadOnlyList<HomeFact>? Facts, string? WaitsOn, HomeRank? Rank, IReadOnlyList<HomeBlocked>? BlocksList, IReadOnlyList<HomeLink>? Links, IReadOnlyList<string>? Actions, HomeSource? Source, IReadOnlyList<HomeEdge>? Edges);
