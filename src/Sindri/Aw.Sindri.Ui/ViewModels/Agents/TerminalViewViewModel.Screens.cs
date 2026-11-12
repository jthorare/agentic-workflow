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
        State = target.CanAttach ? TerminalState.Connecting : TerminalState.CantAttach;
        Side = TerminalSide.List;
        Rows = [];
        IsDimmed = false;
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
