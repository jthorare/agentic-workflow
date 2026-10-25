namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>One entry of the map's accessible list: a feature (a tile of the overview) or a node, by the name the map draws.</summary>
/// <param name="Key">The node's or feature's key.</param>
/// <param name="Label">The words the map shows for it.</param>
/// <param name="Name">The accessible name: the label and what it is.</param>
/// <param name="IsTile">True for a feature of the overview, which Enter opens; false for a node, which Enter selects on the map.</param>
/// <param name="IsEdge">True for an edge (a link between two nodes or two features); Enter goes to the edge's <paramref name="Target"/>.</param>
/// <param name="Target">For an edge, the key of the node or feature it starts from; empty for everything else.</param>
internal sealed record MapItemViewModel(string Key, string Label, string Name, bool IsTile, bool IsEdge = false, string Target = "");
