namespace Aw.Kernel.Data;

/// <summary>The one place that asks whether a path is an executable file, so a dangling link or an unreadable stat answers no instead of throwing.</summary>
public static class ExecutableFiles
{
    private const UnixFileMode AnyExecute = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    /// <summary>Whether <paramref name="path"/> is a file somebody may execute (any existing file on Windows).</summary>
    /// <param name="path">The path to test.</param>
    /// <returns>True when the file exists and has a user, group or other execute bit; false for a missing path, a dangling link or a file that can't be stat-ed.</returns>
    public static bool IsExecutable(string path) => HasMode(path, AnyExecute);

    /// <summary>Whether <paramref name="path"/> is a file any user may execute: its other-execute bit is set (any existing file on Windows).</summary>
    /// <param name="path">The path to test.</param>
    /// <returns>True when the file exists and its other-execute bit is set; false for a missing path, a dangling link or a file that can't be stat-ed.</returns>
    public static bool IsExecutableByOthers(string path) => HasMode(path, UnixFileMode.OtherExecute);

    private static bool HasMode(string path, UnixFileMode bits)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        return OperatingSystem.IsWindows() || ((FileModes.TryRead(path) ?? UnixFileMode.None) & bits) != UnixFileMode.None;
    }
}
