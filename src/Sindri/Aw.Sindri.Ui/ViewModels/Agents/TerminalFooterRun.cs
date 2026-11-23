namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>One word run of the footer; a key name is drawn as a keycap.</summary>
/// <param name="Text">The words or the key name.</param>
/// <param name="IsKey">True for a key name.</param>
internal sealed record TerminalFooterRun(string Text, bool IsKey);
