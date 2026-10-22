namespace Aw.Sindri.Infrastructure.Sessions;

/// <summary>Reads a process's argument block on macOS (KERN_PROCARGS2), where ps's text can't tell an argument from an environment entry.</summary>
internal interface IMacProcessArguments
{
    /// <summary>The process's argv[0] and allowlisted environment.</summary>
    /// <param name="pid">The process.</param>
    /// <returns>The arguments, or null when the process is gone or not readable.</returns>
    public MacArguments? Read(int pid);
}
