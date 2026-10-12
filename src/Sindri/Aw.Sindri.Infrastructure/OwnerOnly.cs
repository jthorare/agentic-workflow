namespace Aw.Sindri.Infrastructure;

/// <summary>Owner-only permissions for Sindri's state (design spec §5.2): directories 0700, files 0600.</summary>
internal static class OwnerOnly
{
    private const UnixFileMode Directory = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode File = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    /// <summary>
    /// Creates <paramref name="path"/> (and parents) if needed. The directory itself is made 0700; parents it creates keep
    /// the umask, and an existing directory is left as its owner set it.
    /// </summary>
    /// <param name="path">The directory.</param>
    internal static void EnsureDirectory(string path)
    {
        if (System.IO.Directory.Exists(path))
        {
            return;
        }

        _ = System.IO.Directory.CreateDirectory(path);
        if (!OperatingSystem.IsWindows())
        {
            System.IO.File.SetUnixFileMode(path, Directory);
        }
    }

    /// <summary>Makes each existing file 0600.</summary>
    /// <param name="paths">The files; missing ones are skipped.</param>
    internal static void RestrictFiles(params string[] paths)
    {
        foreach (var path in paths.Where(System.IO.File.Exists))
        {
            if (!OperatingSystem.IsWindows())
            {
                System.IO.File.SetUnixFileMode(path, File);
            }
        }
    }

    /// <summary>Writes <paramref name="contents"/> to <paramref name="path"/> through a 0600 temp file and a rename, so readers never see half a file.</summary>
    /// <param name="path">The target file.</param>
    /// <param name="contents">The bytes.</param>
    internal static void WriteAtomically(string path, ReadOnlySpan<byte> contents)
    {
        var temp = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{path}.tmp-{Environment.ProcessId}");
        var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = File;
        }

        using (var stream = new FileStream(temp, options))
        {
            stream.Write(contents);
        }

        System.IO.File.Move(temp, path, overwrite: true);
    }
}
