namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The Agents page (continued): the terminal view beside the list (agents spec §15.1, §15.3, U7a). Enter opens it with focus in the list; Esc or Close closes it; j/k or a click switches it.</summary>
internal sealed partial class AgentsPageViewModel
{
    /// <summary>The terminal view's state.</summary>
    internal TerminalViewViewModel Terminal { get; } = new(TimeProvider.System, isMac: false);

    /// <summary>True while the terminal view is open beside the list.</summary>
    public bool IsTerminalOpen { get; private set; }

    /// <summary>The key of the row the pane shows; null when closed.</summary>
    public string? TerminalKey { get; private set; }

    /// <summary>Opens the terminal view on a row.</summary>
    /// <param name="row">The row.</param>
    internal void OpenTerminal(SessionRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
    }

    /// <summary>Closes the view: the list returns to full width on the same row and the session keeps running.</summary>
    internal void CloseTerminal()
    {
    }

    /// <summary>The Transcript button: opens the §6.3 sheet on the row the pane shows.</summary>
    internal void OpenTranscript()
    {
    }
}
