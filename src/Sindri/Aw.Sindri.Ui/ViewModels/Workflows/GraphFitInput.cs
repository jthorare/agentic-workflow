namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>What <see cref="GraphFit"/> lays out: the server's layers (one list of node ids per layer, in order) and the edges between nodes.</summary>
/// <param name="Layers">The node ids of each layer, left to right.</param>
/// <param name="Edges">Each edge as (from, to) node ids; a step with more than one outgoing edge is a fork.</param>
internal sealed record GraphFitInput(IReadOnlyList<IReadOnlyList<string>> Layers, IReadOnlyList<(string From, string To)> Edges)
{
    /// <summary>The ids of steps with more than one outgoing edge: the gap after a layer holding one is wider, and its arms stay in its row.</summary>
    internal IReadOnlySet<string> Forks => Edges.GroupBy(e => e.From, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
}
