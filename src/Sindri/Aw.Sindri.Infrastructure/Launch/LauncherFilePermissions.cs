using Aw.Kernel.Data;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The file may run commands as the user, like a shell rc (launcher spec, permissions): doctor warns when it, or the directory it sits in, can be written by others.</summary>
internal static class LauncherFilePermissions
{
    private const UnixFileMode Others = UnixFileMode.GroupWrite | UnixFileMode.OtherWrite;

    /// <summary>The warnings for <paramref name="path"/>; none for a missing file or on Windows.</summary>
    /// <param name="path">The launcher.json (or the AW_LAUNCHER_CONFIG target).</param>
    /// <returns>One sentence per problem.</returns>
    internal static IReadOnlyList<string> Warnings(string path)
    {
        if (OperatingSystem.IsWindows() || !File.Exists(path))
        {
            return [];
        }

        var warnings = new List<string>();
        var mode = FileModes.TryRead(path);
        if (mode is null)
        {
            warnings.Add($"{path} can't be read (a broken link, or no permission to look at it) (fix: remove it or point it at a real file)");
        }
        else if ((mode.Value & Others) != UnixFileMode.None)
        {
            warnings.Add($"{path} is group- or world-writable (fix: chmod go-w)");
        }

        var parent = Path.GetDirectoryName(Path.GetFullPath(path));
        if (parent is not null && ((FileModes.TryRead(parent) ?? UnixFileMode.None) & Others) != UnixFileMode.None)
        {
            warnings.Add($"{parent} is group- or world-writable (fix: chmod go-w)");
        }

        return warnings;
    }
}
