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
        var temp = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{path}.tmp-{Environment.ProcessId}-{Guid.NewGuid():N}");   // unique: two writers of one path in this process never share a temp file
        var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = File;
        }

        try
        {
            using (var stream = new FileStream(temp, options))
            {
                stream.Write(contents);
            }

            System.IO.File.Move(temp, path, overwrite: true);
        }
        catch
        {
            _ = TryDelete(temp);   // a failed write or move leaves no temp file behind
            throw;
        }
    }

    /// <summary>Deletes <paramref name="path"/>; a missing file, a missing directory or one that can't be removed right now is not an error.</summary>
    /// <param name="path">The file.</param>
    /// <returns>True when the file is gone, false when it could not be removed.</returns>
    internal static bool TryDelete(string path)
    {
        try
        {
            System.IO.File.Delete(path);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Appends <paramref name="contents"/> to <paramref name="path"/>, creating it 0600.</summary>
    /// <param name="path">The file.</param>
    /// <param name="contents">The bytes.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Done.</returns>
    internal static async Task Append(string path, ReadOnlyMemory<byte> contents, CancellationToken cancellationToken)
    {
        var options = new FileStreamOptions { Mode = FileMode.Append, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = File;
        }

        var stream = new FileStream(path, options);
        await using (stream.ConfigureAwait(false))
        {
            await stream.WriteAsync(contents, cancellationToken).ConfigureAwait(false);
        }
    }
}
