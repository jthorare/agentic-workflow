namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>Why the pane opened: "I want to look" (Enter) or "I want to type now" (r, Ctrl+Enter on a permission or login row, a one-off just started).</summary>
internal enum TerminalOpenIntent
{
    /// <summary>Enter on a row: focus starts in the list, except for a one-off the person just started.</summary>
    Look,

    /// <summary>`r` on a tmux row, Ctrl+Enter on a permission-prompt or login row: focus starts in the pane.</summary>
    Type,
}
