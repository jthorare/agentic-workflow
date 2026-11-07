namespace Aw.Kernel.Data;

/// <summary>The one place that reads a Unix mode, so a dangling link or an unreadable stat answers "unknown" instead of throwing (#2233).</summary>
public static class FileModes
{
    /// <summary>The Unix mode of <paramref name="path"/>, following links.</summary>
    /// <param name="path">A file or directory.</param>
    /// <returns>The mode, or null on Windows, for a missing path, a dangling link or a path that can't be stat-ed.</returns>
    public static UnixFileMode? TryRead(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            return File.GetUnixFileMode(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
