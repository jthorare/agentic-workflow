namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>The graph of one run: its cards and edges, before any of it is placed.</summary>
/// <param name="Nodes">The cards, in the line's own order.</param>
/// <param name="Edges">The edges.</param>
/// <param name="BranchesServed">Whether the engine serves the line's branches and their labels; false draws the fallback and its one-line note.</param>
internal sealed record RunGraph(IReadOnlyList<RunNode> Nodes, IReadOnlyList<RunEdge> Edges, bool BranchesServed);
