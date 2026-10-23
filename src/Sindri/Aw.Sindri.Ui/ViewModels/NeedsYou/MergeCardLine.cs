namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>One row of the merge card: a name and its words.</summary>
/// <param name="Name">The row's name.</param>
/// <param name="Value">What it says.</param>
internal sealed record MergeCardLine(string Name, string Value);
