namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>What <see cref="GraphFit"/> lays out: the server's layers (one list of node ids per layer, in order) and the ids of the steps that fork.</summary>
/// <param name="Layers">The node ids of each layer, left to right.</param>
/// <param name="Forks">The ids of steps with more than one outgoing branch; the gap after a layer holding one is wider.</param>
internal sealed record GraphFitInput(IReadOnlyList<IReadOnlyList<string>> Layers, IReadOnlySet<string> Forks);
