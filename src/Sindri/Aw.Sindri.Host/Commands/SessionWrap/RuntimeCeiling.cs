namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>The scope's loose last-resort ceiling (sandbox spec SB7): it is never the clock, only the end of a scope whose wrap or tmux died.</summary>
internal static class RuntimeCeiling
{
    /// <summary>
    /// What 'RuntimeMaxSec' is set to when a suspension ends: the real time elapsed since the scope started, plus the unsuspended time left, plus the longest
    /// single suspension the profile allows. At the start (nothing elapsed, nothing used) this is the plan's 'RuntimeMax', and every resume moves it forward, so
    /// several long parks in a row never cost the run its scope.
    /// </summary>
    /// <param name="elapsed">The real time since the scope started.</param>
    /// <param name="wall">The wall limit (the Step's timeout plus the grace).</param>
    /// <param name="used">The unsuspended time counted so far.</param>
    /// <param name="parkMax">The longest single suspension.</param>
    /// <returns>The value for 'RuntimeMaxSec', counted from the scope's start.</returns>
    internal static TimeSpan Next(TimeSpan elapsed, TimeSpan wall, TimeSpan used, TimeSpan parkMax) => throw new NotSupportedException();
}
