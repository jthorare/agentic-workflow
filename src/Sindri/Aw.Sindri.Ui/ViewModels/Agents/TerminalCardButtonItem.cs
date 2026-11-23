namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>One button of the card or banner as the view draws it.</summary>
/// <param name="Button">Which button.</param>
/// <param name="Label">Its words.</param>
/// <param name="IsFocused">True when it has the focus ring (card focus).</param>
internal sealed record TerminalCardButtonItem(TerminalCardButton Button, string Label, bool IsFocused);
