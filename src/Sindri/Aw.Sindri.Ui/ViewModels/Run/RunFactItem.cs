namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>One line of the inspector's What it needs or What it produced.</summary>
/// <param name="Label">The fact's label, as served.</param>
/// <param name="Summary">Its summary, as served.</param>
/// <param name="Word">Where it stands: Ready, In progress, Not yet or Not produced.</param>
/// <param name="Brush">The brush key of the word.</param>
internal sealed record RunFactItem(string Label, string Summary, string Word, string Brush);
