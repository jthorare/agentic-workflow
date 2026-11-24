namespace Aw.Sindri.Core.Home;

/// <summary>A row a collector wants on Home (home spec §3.1, §8): the whole row as its source says it now, and the edges that leave its object.</summary>
/// <param name="Row">The row, at version 1.</param>
/// <param name="Edges">The edges that leave the row's object.</param>
public sealed record HomeRaise(HomeRow Row, IReadOnlyList<HomeEdge> Edges);
