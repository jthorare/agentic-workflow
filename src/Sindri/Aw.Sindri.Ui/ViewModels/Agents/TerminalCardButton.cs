namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>A button on the card or banner of a state with nothing to type into (agents spec §15.3 "Card buttons").</summary>
internal enum TerminalCardButton
{
    /// <summary>Can't reach Sindri.</summary>
    RetryNow,

    /// <summary>Open in another window.</summary>
    TakeItBack,

    /// <summary>Can't attach: Jump to it.</summary>
    Jump,

    /// <summary>Copy resume command.</summary>
    CopyResume,

    /// <summary>Ended one-off with a worktree.</summary>
    RemoveWorktree,

    /// <summary>Ended one-off.</summary>
    DiscardSession,
}
