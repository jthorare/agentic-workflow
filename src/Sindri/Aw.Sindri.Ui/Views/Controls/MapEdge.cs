namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>One edge as the edges layer draws it.</summary>
/// <param name="Key">A key unique among the edges.</param>
/// <param name="Route">The path, from a port on one node to a port on the other.</param>
/// <param name="Dashed">Whether the edge is suggested and drawn dashed.</param>
internal sealed record MapEdge(string Key, EdgeRoute Route, bool Dashed);
