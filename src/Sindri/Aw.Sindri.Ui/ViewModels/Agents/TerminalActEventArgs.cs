namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The act id of the one-off session a terminal view button acts on (Remove worktree, Discard session).</summary>
internal sealed class TerminalActEventArgs(string actId) : EventArgs
{
    /// <summary>The act id.</summary>
    public string ActId { get; } = actId;
}
