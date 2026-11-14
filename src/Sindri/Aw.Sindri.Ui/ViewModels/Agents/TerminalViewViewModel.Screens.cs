#pragma warning disable MA0182 // see TerminalViewViewModel.cs
namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The terminal view (continued): the screen, the cache of the last 8, switching, the ended freeze and the can't-attach card (agents spec §15.3, §15.5).</summary>
internal sealed partial class TerminalViewViewModel
{
    private const int CacheSize = 8;

    private readonly List<(string Key, IReadOnlyList<string> Rows)> _cache = [];
    private DateTimeOffset? _endedAt;

    /// <summary>True while the screen is stale: after a drop, from the cache, or frozen after the end.</summary>
    public bool IsDimmed { get; private set; }

    /// <summary>The rows of the screen as drawn.</summary>
    public IReadOnlyList<string> Rows { get; private set; } = [];

    public int CachedScreenCount => _cache.Count;

    /// <summary>"Ended 14:02" on an ended pane; empty otherwise.</summary>
    public string EndedText => _endedAt is { } at ? Labels.TerminalEndedAt(at) : string.Empty;

    public bool CanRemoveWorktree => State == TerminalState.Ended && Target is { Kind: TerminalKind.OneOff, WorktreePath.Length: > 0 };

    public bool CanDiscardSession => State == TerminalState.Ended && Target is { Kind: TerminalKind.OneOff };

    public bool CanCopyResume => State is TerminalState.Ended or TerminalState.CantAttach or TerminalState.Gone && Target is { ResumeCommand.Length: > 0 };

    public bool CanJump => State == TerminalState.CantAttach;

    /// <summary>The card's reason: why a terminal can't attach, or that tmux is gone.</summary>
    public string CardReason =>
        State == TerminalState.CantAttach
            ? Target is { CantAttachReason.Length: > 0 } t ? t.CantAttachReason : Labels.TerminalNoPane
            : State == TerminalState.Gone ? Labels.TerminalSessionGone : string.Empty;

    /// <summary>The card's last screen: the transcript's tail.</summary>
    public string CardTail => State == TerminalState.CantAttach ? Target?.TailText ?? string.Empty : string.Empty;

    /// <summary>The buttons of the card or banner in this state, in Tab order; empty when the state has none.</summary>
    public IReadOnlyList<TerminalCardButton> CardButtons => State switch
    {
        TerminalState.CantReachSindri => [TerminalCardButton.RetryNow],
        TerminalState.TakenOver => [TerminalCardButton.TakeItBack],
        TerminalState.CantAttach => [TerminalCardButton.Jump, .. If(CanCopyResume, TerminalCardButton.CopyResume)],
        TerminalState.Gone => [.. If(CanCopyResume, TerminalCardButton.CopyResume)],
        TerminalState.Ended => [.. If(CanCopyResume, TerminalCardButton.CopyResume), .. If(CanRemoveWorktree, TerminalCardButton.RemoveWorktree), .. If(CanDiscardSession, TerminalCardButton.DiscardSession)],
        TerminalState.Connecting or TerminalState.Reconnecting or TerminalState.Starting or TerminalState.Attached => [],
        _ => [],
    };

    /// <summary>The card as the view draws it, buttons with their focus ring.</summary>
    public IReadOnlyList<TerminalCardButtonItem> CardButtonItems => [.. CardButtons.Select((b, i) => new TerminalCardButtonItem(b, ButtonLabel(b), Side == TerminalSide.Card && i == FocusedButton))];

    /// <summary>True when a card sits over the pane (Can't reach, open in another window, Can't attach, Gone).</summary>
    public bool HasCard => State is TerminalState.CantReachSindri or TerminalState.TakenOver or TerminalState.CantAttach or TerminalState.Gone;

    /// <summary>True when the ended banner sits under the frozen screen.</summary>
    public bool HasBanner => State == TerminalState.Ended;

    /// <summary>The card's heading: the state word.</summary>
    public string CardTitle => HasCard ? StateWord : string.Empty;

    /// <summary>The card's sentence under the heading.</summary>
    public string CardBody => State == TerminalState.CantReachSindri ? (_refused ? Labels.TerminalTokenRefused + " " : string.Empty) + Labels.TerminalEscCloses + "." : CardReason;

    /// <summary>The index in <see cref="CardButtons"/> that has focus while <see cref="TerminalSide.Card"/> does.</summary>
    public int FocusedButton { get; private set; }

    /// <summary>The label of a card button.</summary>
    /// <param name="button">The button.</param>
    /// <returns>Its words.</returns>
    public static string ButtonLabel(TerminalCardButton button) => button switch
    {
        TerminalCardButton.RetryNow => Labels.TerminalRetryNow,
        TerminalCardButton.TakeItBack => Labels.TerminalTakeItBack,
        TerminalCardButton.Jump => Labels.TerminalJump,
        TerminalCardButton.CopyResume => Labels.TerminalCopyResume,
        TerminalCardButton.RemoveWorktree => Labels.TerminalRemoveWorktree,
        TerminalCardButton.DiscardSession => Labels.TerminalDiscardSession,
        _ => string.Empty,
    };

    /// <summary>Presses a card button (a click, or Enter on the focused one).</summary>
    /// <param name="button">The button.</param>
    public void Press(TerminalCardButton button)
    {
        switch (button)
        {
            case TerminalCardButton.RetryNow:
                RetryNow();
                break;
            case TerminalCardButton.TakeItBack:
                TakeItBack();
                break;
            case TerminalCardButton.Jump:
                Raise(JumpRequested);
                break;
            case TerminalCardButton.CopyResume:
                Raise(CopyResumeRequested);
                break;
            case TerminalCardButton.RemoveWorktree:
                RemoveWorktree();
                break;
            case TerminalCardButton.DiscardSession:
                DiscardSession();
                break;
            default:
                break;
        }
    }

    private static TerminalCardButton[] If(bool can, TerminalCardButton button) => can ? [button] : [];

    /// <summary>A screen came from the socket.</summary>
    /// <param name="rows">The ANSI lines of the whole screen.</param>
    /// <param name="dead">True when the pane's process has exited.</param>
    public void FrameReceived(IReadOnlyList<string> rows, bool dead)
    {
        Rows = rows;
        if (dead)
        {
            Freeze();
            return;
        }

        IsDimmed = false;
        Refresh();
    }

    /// <summary>The session ended while the view shows it (the row moved to Recently ended).</summary>
    public void SessionEnded() => Freeze();

    /// <summary>Switches the pane to another row: focus stays in the list, the cached screen shows dimmed until a live frame lands.</summary>
    /// <param name="target">The new row's session.</param>
    public void SwitchTo(TerminalTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (Target is { } old && Rows.Count > 0)
        {
            Store(old.Key, Rows);
        }

        Begin(target);
        if (target.CanAttach)
        {
            var hit = _cache.FindIndex(c => string.Equals(c.Key, target.Key, StringComparison.Ordinal));
            if (hit >= 0)
            {
                (Rows, IsDimmed) = (_cache[hit].Rows, true);
            }
        }

        Side = TerminalSide.List;
        Refresh();
    }

    /// <summary>Remove worktree on an ended one-off banner.</summary>
    public void RemoveWorktree()
    {
        if (CanRemoveWorktree && Target is { } t)
        {
            RemoveWorktreeRequested?.Invoke(this, new TerminalActEventArgs(t.ActId));
        }
    }

    /// <summary>Discard session on an ended one-off banner: ends the tmux session.</summary>
    public void DiscardSession()
    {
        if (CanDiscardSession && Target is { } t)
        {
            DiscardRequested?.Invoke(this, new TerminalActEventArgs(t.ActId));
        }
    }

    // Resets the connection and the screen for a new target.
    private void Begin(TerminalTarget target)
    {
        Target = target;
        (_armedUntil, ControlLine) = (null, string.Empty);
        State = target.CanAttach ? TerminalState.Connecting : TerminalState.CantAttach;
        Side = TerminalSide.List;
        Rows = [];
        IsDimmed = false;
        _refused = false;
        _queued.Clear();
        (_agentReported, _lostAt, _retryAt, _attempt, _endedAt, _otherClients) = (false, null, null, 0, null, 0);
    }

    private void Freeze()
    {
        State = TerminalState.Ended;
        Side = TerminalSide.List;
        IsDimmed = true;
        _endedAt = Clock.GetLocalNow();
        _retryAt = null;
        Refresh();
    }

    private void Store(string key, IReadOnlyList<string> rows)
    {
        _ = _cache.RemoveAll(c => string.Equals(c.Key, key, StringComparison.Ordinal));
        _cache.Add((key, rows));
        if (_cache.Count > CacheSize)
        {
            _cache.RemoveAt(0);
        }
    }
}
