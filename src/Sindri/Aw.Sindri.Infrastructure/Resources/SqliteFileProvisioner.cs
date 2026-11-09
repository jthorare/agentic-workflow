namespace Aw.Sindri.Infrastructure.Resources;

/// <summary>Creates the empty SQLite file of a database resource. A zero-byte file is a valid empty database; SQLite writes its header on first use.</summary>
internal static class SqliteFileProvisioner
{
    /// <summary>Creates <paramref name="path"/> 0600 when it is missing; an existing file is left as it is.</summary>
    /// <param name="path">The database file.</param>
    internal static void Create(string path)
    {
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        try
        {
            using var stream = new FileStream(path, options);
        }
        catch (IOException) when (File.Exists(path))
        {
            // already there: provisioning twice changes nothing
        }
    }
}
