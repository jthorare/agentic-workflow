using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Infrastructure.Resources;

/// <summary>Measures a disk folder with a bounded walk.</summary>
internal static class DiskMeter
{
    /// <summary>Largest number of files and folders one walk visits.</summary>
    internal const int FileCap = 100_000;

    /// <summary>Measures <paramref name="folder"/> and checks it against <paramref name="limitBytes"/>. Symlinks are neither followed nor counted; the walk stops once the total passes the limit or <paramref name="entryCap"/> files and folders were seen.</summary>
    /// <param name="name">The disk's name, for the message.</param>
    /// <param name="folder">The disk folder.</param>
    /// <param name="limitBytes">The limit.</param>
    /// <param name="entryCap">Largest number of files and folders one walk visits (<see cref="FileCap"/>; tests pass a small one).</param>
    /// <returns>The bytes used, SND-RES-001 naming used and limit, or SND-RES-007 when the cap stopped the walk under the limit.</returns>
    internal static Result<long> Check(string name, string folder, long limitBytes, int entryCap = FileCap)
    {
        var (used, seen, capped) = Measure(folder, limitBytes, entryCap);
        if (used > limitBytes)
        {
            return SindriErrors.ResourceDiskOverSize.With(string.Create(CultureInfo.InvariantCulture, $"Disk '{name}' holds {used} bytes ({SandboxUnits.FormatSize(used)}), more than its limit of {limitBytes} bytes ({SandboxUnits.FormatSize(limitBytes)})."));
        }

        // why: a stopped walk's total is a floor, not a measurement; passing it would let a disk over its limit through
        return capped
            ? SindriErrors.ResourceDiskUnmeasured.With(string.Create(CultureInfo.InvariantCulture, $"Disk '{name}' holds more than {seen} files and folders, so it can't be measured; {used} bytes ({SandboxUnits.FormatSize(used)}) were counted before the walk stopped. Delete files you don't need from it."))
            : used;
    }

    /// <summary>Sums file sizes under <paramref name="folder"/> until the total passes <paramref name="limitBytes"/> or <paramref name="entryCap"/> entries were seen.</summary>
    private static (long Used, int Seen, bool Capped) Measure(string folder, long limitBytes, int entryCap)
    {
        long used = 0;
        var seen = 0;
        var pending = new Stack<string>();
        if (Directory.Exists(folder))
        {
            pending.Push(folder);
        }

        while (pending.Count > 0)
        {
            foreach (var entry in Entries(pending.Pop()))
            {
                if (++seen > entryCap)
                {
                    return (used, entryCap, true);
                }

                if (entry is DirectoryInfo)
                {
                    pending.Push(entry.FullName);
                    continue;
                }

                used += ((FileInfo)entry).Length;
                if (used > limitBytes)
                {
                    return (used, seen, false);
                }
            }
        }

        return (used, seen, false);
    }

    /// <summary>The folder's entries without symlinks; a folder that can't be read counts as empty (the next run measures it again).</summary>
    private static List<FileSystemInfo> Entries(string dir)
    {
        try
        {
            return [.. new DirectoryInfo(dir).EnumerateFileSystemInfos().Where(e => e.LinkTarget is null && !e.Attributes.HasFlag(FileAttributes.ReparsePoint))];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
