#pragma warning disable MA0182 // see TerminalViewViewModel.cs
namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The terminal view (continued): the Connection rules (agents spec §15.3 "Connection"): retries, the 30 s rule, a second view, keys before `ready`.</summary>
internal sealed partial class TerminalViewViewModel
{
    private static readonly TimeSpan[] _retryDelays = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(15)];

    private static readonly TimeSpan _lostAfter = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan _keyWait = TimeSpan.FromSeconds(1);

    private readonly List<(string Key, DateTimeOffset At)> _queued = [];
    private bool _agentReported;
    private DateTimeOffset? _lostAt;
    private DateTimeOffset? _retryAt;
    private int _attempt;
    private bool _refused;

    /// <summary>How many times the header word flashed because a key was dropped.</summary>
    public int FlashCount { get; private set; }

    /// <summary>The wait before the next retry; null when none is pending.</summary>
    public TimeSpan? RetryIn => _retryAt is { } at && State is TerminalState.Reconnecting or TerminalState.CantReachSindri
        ? TimeSpan.FromTicks(Math.Max(0, (at - Clock.GetUtcNow()).Ticks))
        : null;

    public bool ShowRetryNow => State == TerminalState.CantReachSindri;

    public bool ShowTakeItBack => State == TerminalState.TakenOver;

    /// <summary>The socket is opening.</summary>
    public void SocketOpening()
    {
        if (State is not (TerminalState.Starting or TerminalState.Attached or TerminalState.CantReachSindri or TerminalState.TakenOver))
        {
            State = TerminalState.Connecting;
        }

        Refresh();
    }

    /// <summary>The `ready` message arrived.</summary>
    /// <returns>The keys that waited for it and are still fresh, in order.</returns>
    public IReadOnlyList<string> Ready()
    {
        var now = Clock.GetUtcNow();
        var fresh = _queued.Where(q => now - q.At <= _keyWait).Select(q => q.Key).ToList();
        if (fresh.Count < _queued.Count)
        {
            FlashCount++;
        }

        _queued.Clear();
        _refused = false;
        (_lostAt, _retryAt, _attempt) = (null, null, 0);
        State = Target is { IsStarting: true } && !_agentReported ? TerminalState.Starting : TerminalState.Attached;
        Refresh();
        return fresh;
    }

    /// <summary>The agent wrote its first hook state: Starting ends.</summary>
    public void AgentReportedState()
    {
        _agentReported = true;
        if (State == TerminalState.Starting)
        {
            State = TerminalState.Attached;
        }

        Refresh();
    }

    /// <summary>The socket dropped (no close code, a ping that went unanswered): focus goes back to the list at once.</summary>
    public void SocketDropped()
    {
        var now = Clock.GetUtcNow();
        _lostAt ??= now;
        _queued.Clear();
        Side = TerminalSide.List;
        IsDimmed = true;
        _retryAt = now + _retryDelays[Math.Min(_attempt, _retryDelays.Length - 1)];
        State = LostState(now);
        Refresh();
    }

    /// <summary>The server closed the socket with a code.</summary>
    /// <param name="code">4001 replaced, 4015 no pane, 4016 gone; any other code retries like a drop.</param>
    public void SocketClosed(int code)
    {
        _refused = false;
        switch (code)
        {
            case 4001:
                (State, Side, _retryAt, _lostAt) = (TerminalState.TakenOver, TerminalSide.List, null, null);
                IsDimmed = true;
                _queued.Clear();
                Refresh();
                break;
            case 1008:
                (State, Side, _retryAt, _lostAt) = (TerminalState.CantReachSindri, TerminalSide.List, null, null);
                IsDimmed = true;
                _refused = true;
                _queued.Clear();
                Refresh();
                break;
            case 4015:
                (State, Side, _retryAt) = (TerminalState.CantAttach, TerminalSide.List, null);
                Refresh();
                break;
            case 4016:
                (State, Side, _retryAt) = (TerminalState.Gone, TerminalSide.List, null);
                IsDimmed = true;
                Refresh();
                break;
            default:
                SocketDropped();
                break;
        }
    }

    /// <summary>A key the person typed with focus in the pane.</summary>
    /// <param name="key">The key name or text.</param>
    /// <returns>Sent now, queued until `ready`, or dropped.</returns>
    public TerminalKeyFate SendKey(string key)
    {
        switch (State)
        {
            case TerminalState.Attached or TerminalState.Starting:
                return TerminalKeyFate.Sent;
            case TerminalState.Connecting:
                _queued.Add((key, Clock.GetUtcNow()));
                return TerminalKeyFate.Queued;
            case TerminalState.Reconnecting or TerminalState.CantReachSindri or TerminalState.TakenOver or TerminalState.CantAttach or TerminalState.Gone or TerminalState.Ended:
            default:
                FlashCount++;
                Refresh();
                return TerminalKeyFate.Dropped;
        }
    }

    /// <summary>Moves the clock-driven rules: due retries, the 30 s rule, keys that waited too long.</summary>
    public void Tick()
    {
        var now = Clock.GetUtcNow();
        if (_queued.RemoveAll(q => now - q.At > _keyWait) > 0)
        {
            FlashCount++;
        }

        if (_retryAt is { } due && due <= now && State is TerminalState.Reconnecting or TerminalState.CantReachSindri)
        {
            _attempt++;
            _retryAt = null;
            State = TerminalState.Connecting;
            Raise(SocketRequested);
        }

        if (_lostAt is not null && State is TerminalState.Reconnecting or TerminalState.Connecting or TerminalState.CantReachSindri)
        {
            State = LostState(now) == TerminalState.CantReachSindri ? TerminalState.CantReachSindri : State;
        }

        Refresh();
    }

    /// <summary>Retry now: asks for a socket at once.</summary>
    public void RetryNow()
    {
        _refused = false;
        _attempt++;
        _retryAt = null;
        State = TerminalState.Connecting;
        Refresh();
        Raise(SocketRequested);
    }

    /// <summary>Take it back: opens a new socket, which closes the other window's view with 4001.</summary>
    public void TakeItBack()
    {
        State = TerminalState.Connecting;
        Refresh();
        Raise(SocketRequested);
    }

    private TerminalState LostState(DateTimeOffset now) => _lostAt is { } at && now - at >= _lostAfter ? TerminalState.CantReachSindri : TerminalState.Reconnecting;
}
