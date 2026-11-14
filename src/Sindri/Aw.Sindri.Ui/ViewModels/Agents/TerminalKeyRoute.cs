namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>Where one key goes in the terminal view.</summary>
internal enum TerminalKeyRoute
{
    /// <summary>The view used it (a focus chord, or Esc in the list): nobody else sees it.</summary>
    Handled,

    /// <summary>To the agent: the pane has focus and the key is not a chord.</summary>
    Pane,

    /// <summary>To the page and the shell (j/k, the palette, the G chord): the list has focus, or a modal does.</summary>
    Page,
}
