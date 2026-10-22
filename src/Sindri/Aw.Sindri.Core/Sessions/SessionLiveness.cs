using System.Globalization;

namespace Aw.Sindri.Core.Sessions;

/// <summary>Whether a session record's process is still the same process (collector spec CO5).</summary>
public static class SessionLiveness
{
    /// <summary>True when a record's <paramref name="procStart"/> equals the live process's start ticks.</summary>
    /// <param name="procStart">The record's 'procStart', which Claude stores as a JSON string: the stat start ticks on Linux, ps's lstart text (UTC) on macOS.</param>
    /// <param name="actualTicks">The process's start ticks (stat field 22 on Linux, the start's epoch seconds on macOS).</param>
    /// <returns>False for a missing, unparsable or different value: a recycled pid must not show a ghost.</returns>
    public static bool Matches(string? procStart, long actualTicks) =>
        (long.TryParse(procStart, NumberStyles.None, CultureInfo.InvariantCulture, out var recorded) ? recorded : PsStartTime.ToEpochSeconds(procStart)) == actualTicks;
}
