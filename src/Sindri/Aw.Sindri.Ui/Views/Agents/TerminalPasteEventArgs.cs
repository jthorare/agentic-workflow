namespace Aw.Sindri.Ui.Views.Agents;

/// <summary>Text pasted into the pane (agents spec 15.3): the clipboard's text, never empty.</summary>
/// <param name="text">The text.</param>
internal sealed class TerminalPasteEventArgs(string text) : EventArgs
{
    /// <summary>The clipboard's text.</summary>
    public string Text { get; } = text;
}
