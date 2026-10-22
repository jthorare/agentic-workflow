namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The way from a starting point to a node.</summary>
/// <param name="Start">The starting point it begins at.</param>
/// <param name="Hops">The steps, in order; none when the node is a starting point.</param>
internal sealed record MapRoute(string Start, IReadOnlyList<MapPathHop> Hops);
