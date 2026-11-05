using System.Globalization;

namespace Aw.Sindri.Infrastructure.Locks;

/// <summary>The files a reclaimer killed between its rename and its delete leaves in a slot ('owner.reclaim.N…', 'pid.reclaim.N…'; N is the reclaimer's pid when the name has one).</summary>
internal static class ReclaimLeftovers
{
    private const string Marker = ".reclaim.";

    /// <summary>A name for the claimed copy of <paramref name="path"/> that carries our pid, so a later reclaimer can tell whether we are still alive.</summary>
    internal static string NameFor(string path) =>
        path + Marker + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + "." + Path.GetRandomFileName();

    /// <summary>True when the folder holds nothing but leftovers and every one names a reclaimer pid that is gone on this host.</summary>
    internal static bool AllDead(string directory) =>
        Find(directory) is { Length: > 0 } left && left.All(l => l.Pid is { } pid && !HolderRecords.Alive(pid));

    /// <summary>
    /// Moves each leftover to a name of ours (one winner per file) and deletes it. False when a reclaimer is alive or a pid-less name isn't aged yet; a
    /// failed move throws, which the caller reads as another reclaimer winning. True without touching anything when the folder holds anything else.
    /// </summary>
    internal static bool Clear(string directory, bool aged)
    {
        if (Find(directory) is not { } left)
        {
            return true;
        }

        if (left.Any(l => l.Pid is { } pid ? HolderRecords.Alive(pid) : !aged))
        {
            return false;
        }

        foreach (var (path, _) in left)
        {
            var mine = NameFor(path);
            File.Move(path, mine);
            File.Delete(mine);
        }

        return true;
    }

    // Null when something else is in the folder: not ours to clear.
    private static (string Path, int? Pid)[]? Find(string directory)
    {
        var found = new List<(string, int?)>();
        foreach (var entry in Directory.GetFileSystemEntries(directory))
        {
            var name = Path.GetFileName(entry);
            if (!(name.StartsWith("owner.reclaim.", StringComparison.Ordinal) || name.StartsWith("pid.reclaim.", StringComparison.Ordinal)) || Directory.Exists(entry))
            {
                return null;
            }

            found.Add((entry, PidOf(name)));
        }

        return [.. found];
    }

    // The digits right after the last '.reclaim.', when a '.' or the end follows them; a random name ('x1y2z3w4.abc') has none.
    private static int? PidOf(string name)
    {
        var tail = name.AsSpan(name.LastIndexOf(Marker, StringComparison.Ordinal) + Marker.Length);
        var end = 0;
        while (end < tail.Length && char.IsAsciiDigit(tail[end]))
        {
            end++;
        }

        return end > 0 && (end == tail.Length || tail[end] == '.') && int.TryParse(tail[..end], NumberStyles.None, CultureInfo.InvariantCulture, out var pid) ? pid : null;
    }
}
