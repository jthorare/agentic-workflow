namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>
/// The wall clock of one session (sandbox spec SB7, §3.3): counts only the time the run was not suspended. A park or a takeover stops it; it resumes with what
/// remained. An interval counts only when the run was unsuspended at both of its ends, so an edge never costs the run more than the tick it fell in.
/// </summary>
/// <param name="limit">The wall limit.</param>
internal sealed class WallBudget(TimeSpan limit)
{
    /// <summary>Gets the unsuspended time counted so far.</summary>
    internal TimeSpan Used => throw new NotSupportedException();

    /// <summary>Gets the unsuspended time left, never below zero.</summary>
    internal TimeSpan Left => throw new NotSupportedException();

    /// <summary>Gets a value indicating whether the limit is used up.</summary>
    internal bool Exceeded => limit == TimeSpan.MinValue ? false : throw new NotSupportedException();

    /// <summary>Records one look at the run.</summary>
    /// <param name="now">The time of the look.</param>
    /// <param name="suspended">Whether the run was suspended at that time.</param>
    internal void Tick(DateTimeOffset now, bool suspended) => throw new NotSupportedException();
}
