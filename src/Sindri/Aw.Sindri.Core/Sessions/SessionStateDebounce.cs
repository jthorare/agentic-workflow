namespace Aw.Sindri.Core.Sessions;

/// <summary>
/// Which state changes of a pass reach 'session.changed' (Agents page spec §7.2, issue 3146): a new state counts once it has held for <see cref="HoldFor"/> (the wait 'fleet.changed' uses for asking), and a flip
/// back to the last emitted state inside that time emits nothing. A session new to the debounce takes its state as the baseline: that is 'fleet.changed' appeared, not a change here. Keep one per collector;
/// the host's loop is its only caller.
/// </summary>
public sealed class SessionStateDebounce
{
    /// <summary>How long a new state must hold before it is a change.</summary>
    public static readonly TimeSpan HoldFor = TimeSpan.FromSeconds(5);

    private readonly Dictionary<SessionRef, SessionState> _emitted = [];
    private readonly Dictionary<SessionRef, (SessionState State, DateTimeOffset Since)> _waiting = [];

    /// <summary>Takes a good pass and returns the changes whose new state has now held long enough, ordered by ref.</summary>
    /// <param name="now">This pass's sessions.</param>
    /// <param name="at">The time of the pass.</param>
    /// <returns>The changes.</returns>
    public IReadOnlyList<SessionStateChange> Settle(IReadOnlyList<ObservedSession> now, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(now);
        var live = now.Select(static s => s.Ref).ToHashSet();
        foreach (var gone in _emitted.Keys.Where(r => !live.Contains(r)).ToList())
        {
            _ = _emitted.Remove(gone);
            _ = _waiting.Remove(gone);
        }

        var changes = new List<SessionStateChange>();
        foreach (var session in now.OrderBy(static s => s.Ref.ToString(), StringComparer.Ordinal))
        {
            if (Settled(session, at) is { } change)
            {
                changes.Add(change);
            }
        }

        return changes;
    }

    private SessionStateChange? Settled(ObservedSession session, DateTimeOffset at)
    {
        if (!_emitted.TryGetValue(session.Ref, out var last))
        {
            _emitted[session.Ref] = session.State;
            return null;
        }

        if (session.State == last)
        {
            _ = _waiting.Remove(session.Ref);   // back to the emitted state: the flip never held
            return null;
        }

        if (!_waiting.TryGetValue(session.Ref, out var wait) || wait.State != session.State)
        {
            _waiting[session.Ref] = wait = (session.State, at);
        }

        if (at - wait.Since < HoldFor)
        {
            return null;
        }

        _emitted[session.Ref] = session.State;
        _ = _waiting.Remove(session.Ref);
        return new SessionStateChange(session.Ref, session.State, at);
    }
}
