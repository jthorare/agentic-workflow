namespace Aw.Sindri.Core.Sessions;

/// <summary>Which state changes of a pass reach 'session.changed' (Agents page spec §7.2). Pure.</summary>
public static class SessionStateChanges
{
    /// <summary>One change per session that was in the last good pass with another state. A session new to the pass is 'fleet.changed' appeared, not a change here.</summary>
    /// <param name="previous">The last good pass's sessions.</param>
    /// <param name="now">This pass's sessions.</param>
    /// <param name="at">The time of the pass.</param>
    /// <returns>The changes, by ref.</returns>
    public static IReadOnlyList<SessionStateChange> Diff(IReadOnlyDictionary<SessionRef, ObservedSession> previous, IReadOnlyList<ObservedSession> now, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(now);
        return [.. now.Where(s => previous.TryGetValue(s.Ref, out var before) && before.State != s.State)
            .OrderBy(static s => s.Ref.ToString(), StringComparer.Ordinal)
            .Select(s => new SessionStateChange(s.Ref, s.State, at))];
    }
}
