using System.Globalization;
using Aw.Sindri.Infrastructure.Locks;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// The files and signals of a session that runs as its own process group (macOS has no cgroups, so the group is the session's "unit"; SB-9b).
/// They live in one flat folder, '&lt;launch dir&gt;/pgroups' (<see cref="Directory"/>), beside 'areas' and 'denials', not inside the launch's run area: the run area
/// is removed with the launch, while the slice count and the memory-kill record must still be readable after the session ends. The launcher (SB-9c) writes
/// '&lt;unit&gt;.pgid', the group id as one decimal line, as it starts the session; the watchdog writes '&lt;pgid&gt;.oom' when it kills for memory. The unit is the launch id,
/// so a file name; a unit with a path separator in it is no unit.
/// </summary>
internal static class ProcessGroupFiles
{
    private const int Kill = 9;
    private const int Hangup = 1;
    private const int Interrupt = 2;
    private const int Terminate = 15;

    /// <summary>The folder of the group files.</summary>
    /// <param name="launchDirectory">The launcher's directory.</param>
    /// <returns>The folder.</returns>
    internal static string Directory(string launchDirectory) => Path.Combine(launchDirectory, "pgroups");

    /// <summary>Writes the group id of a unit, replacing an older one and the memory-kill record of the group id it names.</summary>
    /// <param name="directory">The folder of the group files.</param>
    /// <param name="unit">The unit.</param>
    /// <param name="pgid">The process group id; more than 1.</param>
    internal static void WritePgid(string directory, string unit, int pgid)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pgid, 1);
        _ = System.IO.Directory.CreateDirectory(directory);
        File.Delete(MarkerFile(directory, pgid));   // why: a group id is reused by the system, a record of an older group must not blame this one
        var file = PgidFile(directory, unit) ?? throw new ArgumentException("the unit is not a file name", nameof(unit));
        var temporary = file + ".tmp";
        File.WriteAllText(temporary, pgid.ToString(CultureInfo.InvariantCulture) + "\n");
        File.Move(temporary, file, overwrite: true);
    }

    /// <summary>Reads the group id of a unit.</summary>
    /// <param name="directory">The folder of the group files.</param>
    /// <param name="unit">The unit.</param>
    /// <returns>The group id, or null when the file is missing, malformed, or names 1 or less (0 and 1 would signal the caller's own group or every process).</returns>
    internal static int? ReadPgid(string directory, string unit)
    {
        if (PgidFile(directory, unit) is not { } file)
        {
            return null;
        }

        try
        {
            return int.TryParse(File.ReadAllText(file).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var pgid) && pgid > 1 ? pgid : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Tells whether any process of the group is still there (a zombie counts until it is reaped).</summary>
    /// <param name="pgid">The process group id.</param>
    /// <returns>True when the group has a process.</returns>
    internal static bool Alive(int pgid)
    {
        var error = Libc.Signal(-pgid, 0);
        return error is 0 or Libc.NotPermitted;
    }

    /// <summary>Signals every process of the group.</summary>
    /// <param name="pgid">The process group id.</param>
    /// <param name="signal">The signal name: SIGKILL, SIGTERM, SIGINT or SIGHUP.</param>
    /// <returns>True when the signal was sent; false for another signal name or a group that is gone.</returns>
    internal static bool Signal(int pgid, string signal)
    {
        int? number = signal switch
        {
            "SIGKILL" => Kill,
            "SIGTERM" => Terminate,
            "SIGINT" => Interrupt,
            "SIGHUP" => Hangup,
            _ => null,
        };
        return number is { } n && pgid > 1 && Libc.Signal(-pgid, n) == 0;
    }

    /// <summary>Writes down that the watchdog killed the group for memory.</summary>
    /// <param name="directory">The folder of the group files.</param>
    /// <param name="pgid">The process group id.</param>
    internal static void RecordMemoryKill(string directory, int pgid)
    {
        _ = System.IO.Directory.CreateDirectory(directory);
        File.WriteAllText(MarkerFile(directory, pgid), "oom-kill\n");
    }

    /// <summary>Tells whether the watchdog killed the group for memory.</summary>
    /// <param name="directory">The folder of the group files.</param>
    /// <param name="pgid">The process group id.</param>
    /// <returns>True when the record is there.</returns>
    internal static bool MemoryKilled(string directory, int pgid) => File.Exists(MarkerFile(directory, pgid));

    /// <summary>Lists the units that have a group id file, in order.</summary>
    /// <param name="directory">The folder of the group files.</param>
    /// <returns>The units; empty when the folder is not there.</returns>
    internal static IReadOnlyList<string> Units(string directory)
    {
        try
        {
            return [.. System.IO.Directory.EnumerateFiles(directory, "*.pgid").Select(f => Path.GetFileNameWithoutExtension(f)).Order(StringComparer.Ordinal)];
        }
        catch (DirectoryNotFoundException)
        {
            return [];
        }
    }

    private static string? PgidFile(string directory, string unit) =>
        unit.Length == 0 || unit.AsSpan().ContainsAny('/', '\\') || unit.Contains("..", StringComparison.Ordinal) ? null : Path.Combine(directory, unit + ".pgid");

    private static string MarkerFile(string directory, int pgid) => Path.Combine(directory, pgid.ToString(CultureInfo.InvariantCulture) + ".oom");
}
