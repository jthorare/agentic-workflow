#pragma warning disable MA0182 // the Agents page wires the view in the U1 page PR; until then only tests construct it
using Aw.Sindri.Ui.Terminal;

namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>
/// The page side of the live pane (agents spec §15.3, U7a): which side has focus, the footer and header words, the Connection rules, the cache of
/// dimmed screens, the ended freeze and the can't-attach card. It draws nothing and opens no socket: a socket source reports to it and the
/// drawing (U7b) reads it. Partial files: focus, connection, screens.
/// </summary>
internal sealed partial class TerminalViewViewModel : ObservableObject
{
    private static readonly string[] _allProperties =
    [
        nameof(Target), nameof(Side), nameof(State), nameof(StateWord), nameof(HeaderName), nameof(HostText), nameof(FooterText),
        nameof(IsPaneAccented), nameof(IsListAccented), nameof(IsCursorHollow), nameof(ShellKeysEnabled), nameof(PaletteEnabled),
        nameof(IsDimmed), nameof(Rows), nameof(CachedScreenCount), nameof(FlashCount), nameof(RetryIn), nameof(ShowRetryNow),
        nameof(ShowTakeItBack), nameof(HintText), nameof(EndedText), nameof(CanRemoveWorktree), nameof(CanDiscardSession),
        nameof(CanCopyResume), nameof(CanJump), nameof(CardReason), nameof(CardTail), nameof(IsCardAccented), nameof(CardButtons),
        nameof(FocusedButton), nameof(ShowStop), nameof(CardButtonItems), nameof(HasCard), nameof(HasBanner), nameof(CardTitle), nameof(CardBody), nameof(CanStop), nameof(CanInterrupt), nameof(IsStopArmed), nameof(StopLabel), nameof(StopAutomationId),
        nameof(HeaderStateWord), nameof(IsStateAttention), nameof(IsStateError), nameof(IsStateEnded), nameof(HasCardTail), nameof(FooterParts),
    ];

    private readonly ISessionControl? _control;
    private int _otherClients;

    internal TerminalViewViewModel(TimeProvider clock, bool isMac, ISessionControl? control = null)
    {
        _control = control;
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

    /// <summary>Raised by Jump to it on the can't-attach card.</summary>
    internal event EventHandler? JumpRequested;

    /// <summary>Raised by Copy resume command.</summary>
    internal event EventHandler? CopyResumeRequested;

    internal TimeProvider Clock { get; }

    internal bool IsMac { get; }

    /// <summary>The session the view shows; null before the first open.</summary>
    public TerminalTarget? Target { get; private set; }

    /// <summary>The state word's meaning.</summary>
    public TerminalState State { get; private set; } = TerminalState.Connecting;

    /// <summary>The one state word of the header.</summary>
    public string StateWord => State switch
    {
        TerminalState.Connecting => Labels.TerminalConnecting,
        TerminalState.Reconnecting => Labels.TerminalReconnecting,
        TerminalState.CantReachSindri => Labels.TerminalCantReach,
        TerminalState.TakenOver => Labels.TerminalTakenOver,
        TerminalState.Starting => Labels.TerminalStarting,
        TerminalState.Attached => Labels.TerminalAttached,
        TerminalState.CantAttach => Labels.TerminalCantAttach,
        TerminalState.Gone => Labels.TerminalGone,
        TerminalState.Ended => Labels.TerminalEnded,
        _ => Labels.TerminalEnded,
    };

    /// <summary>The header's state word: <see cref="StateWord"/>, except that the open-in-another-window card carries its own sentence and the header stays blank (DG2 frame 5).</summary>
    public string HeaderStateWord => State == TerminalState.TakenOver ? string.Empty : StateWord;

    /// <summary>True when the state dot and word are orange (DG2: Connecting, Reconnecting, Starting, Can't attach).</summary>
    public bool IsStateAttention => State is TerminalState.Connecting or TerminalState.Reconnecting or TerminalState.Starting or TerminalState.CantAttach;

    /// <summary>True when the state dot and word are red (DG2: Can't reach Sindri, Gone).</summary>
    public bool IsStateError => State is TerminalState.CantReachSindri or TerminalState.Gone;

    /// <summary>True when the state dot is grey (DG2: Ended).</summary>
    public bool IsStateEnded => State == TerminalState.Ended;

    /// <summary>True when the card has a tail block to show.</summary>
    public bool HasCardTail => !string.IsNullOrEmpty(CardTail);

    /// <summary>The target's name, drawn at 16 px.</summary>
    public string HeaderName => Target?.Name ?? string.Empty;

    /// <summary>"tmux default · 1 other client"; empty for a host with no pane.</summary>
    public string HostText => Target is { SocketKind.Length: > 0 } t ? Labels.TerminalHost(t.SocketKind, State is TerminalState.Ended or TerminalState.CantReachSindri ? 0 : _otherClients) : string.Empty;

    /// <summary>Where the 30 s rule shows "Esc closes the view"; empty otherwise.</summary>
    public string HintText => State == TerminalState.CantReachSindri ? Labels.TerminalEscCloses : string.Empty;

    /// <summary>Records how many other clients are attached to the session (from AttachedClients).</summary>
    /// <param name="count">The number of other clients.</param>
    public void SetOtherClients(int count)
    {
        _otherClients = count;
        Refresh();
    }

    private bool CanTakeFocus => State is TerminalState.Attached or TerminalState.Starting;

    /// <summary>False in Can't reach Sindri, Gone and a Can't attach card: there is no process Sindri can stop from here. Take it back keeps Stop (#3400).</summary>
    public bool ShowStop => State is not (TerminalState.CantReachSindri or TerminalState.CantAttach or TerminalState.Gone);

    private void Refresh()
    {
        if (Side == TerminalSide.Card && CardButtons.Count == 0)
        {
            Side = TerminalSide.List;
        }

        FocusedButton = Math.Clamp(FocusedButton, 0, Math.Max(0, CardButtons.Count - 1));
        foreach (var name in _allProperties)
        {
            OnChanged(name);
        }
    }

    private void Raise(EventHandler? handler) => handler?.Invoke(this, EventArgs.Empty);
}
