namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The wrapped layout and the zoom that fits it.</summary>
/// <param name="Zoom">The Fit zoom: 1 at most, never below <see cref="GraphFit.Floor"/>.</param>
/// <param name="Cards">Each node's card in the world (laid out at the text scale, before <paramref name="Zoom"/>).</param>
/// <param name="Columns">The most layers in one row.</param>
/// <param name="Rows">The number of rows.</param>
/// <param name="Width">The world's width, right padding included.</param>
/// <param name="Height">The world's height, the 24 px bottom margin included once.</param>
/// <param name="RowOf">The row each node sits in, from 0.</param>
/// <param name="ReturnLanes">The lines that run back from the end of a row to the start of the next, each part a thin box in the world.</param>
internal sealed record GraphFitResult(double Zoom, IReadOnlyDictionary<string, GraphBox> Cards, int Columns, int Rows, double Width, double Height, IReadOnlyList<GraphBox> ReturnLanes, IReadOnlyDictionary<string, int> RowOf);
