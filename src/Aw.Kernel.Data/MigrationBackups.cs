using System.Globalization;

namespace Aw.Kernel.Data;

/// <summary>The upgrade backups MigrationRunner writes next to a database: their mode and their pruning.</summary>
internal static class MigrationBackups
{
    internal static void RestrictToOwner(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // best effort: a backup that stays group-readable is no reason to fail the migration
        }
    }

    // Best effort after a successful upgrade: keeps the newest backup and the one this run wrote or kept. They differ
    // only when a higher version's backup is already there (the database was restored below it), and that file is
    // then the only copy of the later data.
    internal static void Prune(string dataSource, int current)
    {
        var path = Path.GetFullPath(dataSource);
        var prefix = $"{Path.GetFileName(path)}.bak-v";
        var found = new List<(string File, int Version)>();
        try
        {
            foreach (var file in Directory.EnumerateFiles(Path.GetDirectoryName(path) ?? ".", prefix + "*"))
            {
                var digits = Path.GetFileName(file)[prefix.Length..];
                if (digits.Length > 0 && digits.All(char.IsAsciiDigit) && int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var version))
                {
                    found.Add((file, version));
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return;
        }

        var newest = found.Count == 0 ? 0 : found.Max(f => f.Version);
        foreach (var (file, version) in found.Where(f => f.Version != newest && f.Version != current))
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // best effort: the next successful upgrade tries again
            }
        }
    }
}
