namespace Aw.Judge.Infrastructure.Processes;

/// <summary>Finds an executable in the PATH directories: a regular file with an execute bit (any file on Windows).</summary>
internal static class SearchPath
{
    private const UnixFileMode AnyExecute = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    /// <summary>The first executable of this name on the PATH, or null.</summary>
    /// <param name="path">The PATH value (null when unset).</param>
    /// <param name="name">The binary name.</param>
    /// <returns>Its full path, or null.</returns>
    internal static string? Find(string? path, string name) =>
        (path ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, name))
            .FirstOrDefault(IsExecutable);

    /// <summary>Whether a file is a regular file the user may execute. A file it can't stat (a dangling link, no permission) is not executable.</summary>
    /// <param name="file">The file.</param>
    /// <returns>True when it is.</returns>
    internal static bool IsExecutable(string file)
    {
        if (!File.Exists(file))
        {
            return false;
        }

        if (OperatingSystem.IsWindows())
        {
            return true;
        }

        try
        {
            return (File.GetUnixFileMode(file) & AnyExecute) != UnixFileMode.None;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
