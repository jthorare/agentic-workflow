using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Infrastructure.Resources;

#pragma warning disable MA0182 // until the launch path provisions resources before a launch (agent-first PR 14 wires only the tests)

/// <summary>Measures a disk folder with a bounded walk.</summary>
internal static class DiskMeter
{
    /// <summary>Largest number of files one walk visits.</summary>
    internal const int FileCap = 100_000;

    /// <summary>Measures <paramref name="folder"/> and checks it against <paramref name="limitBytes"/>. Symlinks are neither followed nor counted; the walk stops once the total passes the limit.</summary>
    /// <param name="name">The disk's name, for the message.</param>
    /// <param name="folder">The disk folder.</param>
    /// <param name="limitBytes">The limit.</param>
    /// <returns>The bytes used, or SND-RES-001 naming used and limit.</returns>
    internal static Result<long> Check(string name, string folder, long limitBytes)
    {
        var used = Measure(folder, limitBytes);
        return used > limitBytes
            ? SindriErrors.ResourceDiskOverSize.With(string.Create(CultureInfo.InvariantCulture, $"Disk '{name}' holds {used} bytes ({SandboxUnits.FormatSize(used)}), more than its limit of {limitBytes} bytes ({SandboxUnits.FormatSize(limitBytes)})."))
            : used;
    }

    /// <summary>Sums file sizes under <paramref name="folder"/> until the total passes <paramref name="limitBytes"/> or <see cref="FileCap"/> files were seen.</summary>
    private static long Measure(string folder, long limitBytes)
    {
        long used = 0;
        var seen = 0;
        var pending = new Stack<string>();
        if (Directory.Exists(folder))
        {
            pending.Push(folder);
        }

        while (pending.Count > 0 && used <= limitBytes && seen < FileCap)
        {
            foreach (var entry in Entries(pending.Pop()))
            {
                if (entry is DirectoryInfo)
                {
                    pending.Push(entry.FullName);
                    continue;
                }

                seen++;
                used += ((FileInfo)entry).Length;
                if (used > limitBytes || seen >= FileCap)
                {
                    break;
                }
            }
        }

        return used;
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
#pragma warning restore MA0182
