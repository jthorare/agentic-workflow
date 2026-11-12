#pragma warning disable MA0041, CA1822, CS0067, MA0182 // red stub: members throw until the behavior lands
namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>
/// The page side of the live pane (agents spec §15.3, U7a): which side has focus, the footer and header words, the Connection rules, the cache of
/// dimmed screens, the ended freeze and the can't-attach card. It draws nothing and opens no socket: a socket source reports to it and the
/// drawing (U7b) reads it.
/// </summary>
internal sealed class TerminalViewViewModel : ObservableObject
{
    internal TerminalViewViewModel(TimeProvider clock, bool isMac)
    {
        Clock = clock;
        IsMac = isMac;
    }

    /// <summary>Raised when the view wants a socket: a retry came due, Retry now, or Take it back.</summary>
    internal event EventHandler? SocketRequested;

    /// <summary>Raised when the view closes (Esc in the list, Close).</summary>
    internal event EventHandler? CloseRequested;

    /// <summary>Raised by Remove worktree with the act id.</summary>
    internal event EventHandler<TerminalActEventArgs>? RemoveWorktreeRequested;

    /// <summary>Raised by Discard session with the act id.</summary>
    internal event EventHandler<TerminalActEventArgs>? DiscardRequested;

    internal TimeProvider Clock { get; }

    internal bool IsMac { get; }

    public TerminalTarget? Target => throw new NotSupportedException();

    public TerminalSide Side => throw new NotSupportedException();

    public TerminalState State => throw new NotSupportedException();

    /// <summary>The one state word of the header.</summary>
    public string StateWord => throw new NotSupportedException();

    /// <summary>The target's name, drawn at 16 px.</summary>
    public string HeaderName => throw new NotSupportedException();

    /// <summary>"tmux default · 1 other client".</summary>
    public string HostText => throw new NotSupportedException();

    public string FooterText => throw new NotSupportedException();

    public bool IsPaneAccented => throw new NotSupportedException();

    public bool IsListAccented => throw new NotSupportedException();

    public bool IsCursorHollow => throw new NotSupportedException();

    /// <summary>False while the pane has focus: page and shell keys, the G chord included, are off.</summary>
    public bool ShellKeysEnabled => throw new NotSupportedException();

    /// <summary>False while the pane has focus: Ctrl+K goes to the agent.</summary>
    public bool PaletteEnabled => throw new NotSupportedException();

    public bool IsDimmed => throw new NotSupportedException();

    public IReadOnlyList<string> Rows => throw new NotSupportedException();

    public int CachedScreenCount => throw new NotSupportedException();

    /// <summary>How many times the header word flashed because a key was dropped.</summary>
    public int FlashCount => throw new NotSupportedException();

    /// <summary>The wait before the next retry; null when none is pending.</summary>
    public TimeSpan? RetryIn => throw new NotSupportedException();

    public bool ShowRetryNow => throw new NotSupportedException();

    public bool ShowTakeItBack => throw new NotSupportedException();

    /// <summary>"Esc closes the view" while the socket is lost for good, else empty.</summary>
    public string HintText => throw new NotSupportedException();

    /// <summary>"Ended 14:02" on an ended pane, else empty.</summary>
    public string EndedText => throw new NotSupportedException();

    public bool CanRemoveWorktree => throw new NotSupportedException();

    public bool CanDiscardSession => throw new NotSupportedException();

    public bool CanCopyResume => throw new NotSupportedException();

    public bool CanJump => throw new NotSupportedException();

    /// <summary>The can't-attach card's reason.</summary>
    public string CardReason => throw new NotSupportedException();

    /// <summary>The can't-attach card's last screen.</summary>
    public string CardTail => throw new NotSupportedException();

    public void Open(TerminalTarget target, TerminalOpenIntent intent) => throw new NotSupportedException();

    public void SwitchTo(TerminalTarget target) => throw new NotSupportedException();

    public void PressEnterOnShownRow() => throw new NotSupportedException();

    public void ClickPane() => throw new NotSupportedException();

    public void ClickList() => throw new NotSupportedException();

    /// <summary>Routes one key: "F6", "Shift+F6", "Ctrl+Shift+L", "Cmd+Shift+L", "Esc", or any other key name.</summary>
    public TerminalKeyRoute Route(string key) => throw new NotSupportedException();

    public void OpenModal() => throw new NotSupportedException();

    public void CloseModal() => throw new NotSupportedException();

    public void SocketOpening() => throw new NotSupportedException();

    /// <summary>The `ready` message arrived.</summary>
    /// <returns>The keys that waited for it, in order.</returns>
    public IReadOnlyList<string> Ready() => throw new NotSupportedException();

    public void FrameReceived(IReadOnlyList<string> rows, bool dead) => throw new NotSupportedException();

    public void AgentReportedState() => throw new NotSupportedException();

    public void SocketDropped() => throw new NotSupportedException();

    public void SocketClosed(int code) => throw new NotSupportedException();

    public void SetOtherClients(int count) => throw new NotSupportedException();

    public TerminalKeyFate SendKey(string key) => throw new NotSupportedException();

    /// <summary>Moves the clock-driven rules: due retries, the 30 s rule, keys that waited too long.</summary>
    public void Tick() => throw new NotSupportedException();

    public void RetryNow() => throw new NotSupportedException();

    public void TakeItBack() => throw new NotSupportedException();

    public void SessionEnded() => throw new NotSupportedException();

    public void RemoveWorktree() => throw new NotSupportedException();

    public void DiscardSession() => throw new NotSupportedException();

    public void Close() => throw new NotSupportedException();
}
