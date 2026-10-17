namespace Aw.Forge.Tests;

internal static class Executable
{
    /// <summary>Marks a script runnable. The tests that use it run on Linux and macOS (they need sh and git).</summary>
    internal static void Make(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
}
