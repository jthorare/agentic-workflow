namespace Aw.Sindri.Core.Sessions;

/// <summary>One process of the table (collector spec §4.1 step 1): what /proc/&lt;pid&gt;/stat and the argv[0] and exe links give (on macOS, ps and the argument block).</summary>
/// <param name="Pid">The process id.</param>
/// <param name="ParentPid">Its parent's id.</param>
/// <param name="Comm">The kernel's name for it (cut to 15 characters).</param>
/// <param name="Tty">The controlling terminal's number (stat's tty_nr), 0 for none; on macOS 0 for ps's '??' and a nonzero number for a terminal, which is all the collector asks.</param>
/// <param name="StartTicks">Its start time in clock ticks (stat field 22), or on macOS in epoch seconds (ps's lstart in UTC): with the pid it identifies the process.</param>
public sealed record ProcessEntry(int Pid, int ParentPid, string Comm, int Tty, long StartTicks)
{
    /// <summary>The program name argv[0] gives (read to its first NUL only), or null when unreadable.</summary>
    public string? Argv0 { get; init; }

    /// <summary>The executable's path (the exe link's target), or null when unreadable.</summary>
    public string? ExePath { get; init; }
}
