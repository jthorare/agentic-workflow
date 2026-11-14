namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>A branch of a condition step that leads nowhere: the canvas draws "false → done" there, so a missing edge reads as the run ending.</summary>
/// <param name="Node">The condition step's id.</param>
/// <param name="Text">The words drawn, such as "false → done".</param>
/// <param name="At">Where the dotted stub leaves the card.</param>
/// <param name="To">Where the dotted stub ends.</param>
/// <param name="Label">The box the words are drawn in.</param>
internal sealed record GraphEnd(string Node, string Text, GraphPoint At, GraphPoint To, GraphBox Label);
