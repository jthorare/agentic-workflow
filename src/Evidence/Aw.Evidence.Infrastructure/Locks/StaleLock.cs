using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace Aw.Evidence.Infrastructure.Locks;

/// <summary>
/// Frees a lock directory whose holder is dead, as config/lib/locks.sh does: the holder file is moved aside first (only one
/// waiter's move wins) and the directory is freed only if what was moved is still the dead holder's line. A directory with
/// no readable holder (a holder between its mkdir and its pid write, an unreadable line) is never freed.
/// </summary>
internal static class StaleLock
{
    /// <summary>Frees <paramref name="directory"/> when the pid it records names a process that is gone.</summary>
    /// <param name="directory">The lock slot.</param>
    /// <returns>True when the directory is gone and the slot can be taken.</returns>
    internal static bool TryReclaim(string directory)
    {
        var holder = HolderFile(directory);
        if (holder is null || !TryRead(holder, out var line) || PidOf(line) is not { } pid || IsAlive(pid))
        {
            return false;
        }

        var moved = string.Create(CultureInfo.InvariantCulture, $"{holder}.reclaim.{Environment.ProcessId}");
        if (!TryMove(holder, moved))
        {
            return false;
        }

        if (!TryRead(moved, out var seen) || !string.Equals(seen, line, StringComparison.Ordinal))
        {
            _ = TryMove(moved, holder);   // the slot changed hands in between
            return false;
        }

        TryDelete(moved);
        TryDelete(Path.Combine(directory, holder.EndsWith(DirectoryLocks.PidFile, StringComparison.Ordinal) ? DirectoryLocks.OwnerFile : DirectoryLocks.PidFile));
        return TryRemove(directory);
    }

    /// <summary>The file naming the holder: the shell's 'pid', else the C# holder's 'owner' line (its first word is the pid).</summary>
    private static string? HolderFile(string directory) =>
        new[] { DirectoryLocks.PidFile, DirectoryLocks.OwnerFile }.Select(n => Path.Combine(directory, n)).FirstOrDefault(File.Exists);

    private static int? PidOf(string line)
    {
        var word = line.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return int.TryParse(word, NumberStyles.None, CultureInfo.InvariantCulture, out var pid) && pid > 0 ? pid : null;
    }

    /// <summary>Alive unless the process is gone; like 'kill -0', a process that can't be inspected counts as alive.</summary>
    private static bool IsAlive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return true;
        }
        catch (ArgumentException)
        {
            return false;   // no such process
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return true;
        }
    }

    private static bool TryRead(string file, out string text)
    {
        try
        {
            text = File.ReadAllText(file);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            text = string.Empty;
            return false;
        }
    }

    private static bool TryMove(string from, string to)
    {
        try
        {
            File.Move(from, to, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // best effort: a leftover file keeps the directory, and the next waiter tries again
        }
    }

    private static bool TryRemove(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
