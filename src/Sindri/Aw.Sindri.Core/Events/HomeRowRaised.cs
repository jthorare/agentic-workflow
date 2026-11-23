using Aw.Sindri.Core.Home;

namespace Aw.Sindri.Core.Events;

/// <summary>A collector found an object that needs the owner (home spec §3.1): the whole row, and the edges it adds to the link graph.</summary>
/// <param name="Row">The row, at version 1.</param>
/// <param name="Edges">The edges that leave the row's object.</param>
public sealed record HomeRowRaised(HomeRow Row, IReadOnlyList<HomeEdge> Edges);
