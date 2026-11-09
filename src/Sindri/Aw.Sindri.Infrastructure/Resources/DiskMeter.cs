using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Infrastructure.Resources;

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
        long used = 0;
        var seen = 0;
        var pending = new Stack<string>();
        if (Directory.Exists(folder))
        {
            pending.Push(folder);
        }

        while (pending.Count > 0 && used <= limitBytes && seen < FileCap)
        {
            var dir = pending.Pop();
            IEnumerable<FileSystemInfo> entries;
            try
            {
                entries = new DirectoryInfo(dir).EnumerateFileSystemInfos().ToList();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                continue;   // a folder that can't be read counts as empty: the next run measures it again
            }

            foreach (var entry in entries)
            {
                if (entry.LinkTarget is not null || entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    continue;
                }

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

        return used > limitBytes
            ? SindriErrors.ResourceDiskOverSize.With($"Disk '{name}' holds {used} bytes ({SandboxUnits.FormatSize(used)}), more than its limit of {limitBytes} bytes ({SandboxUnits.FormatSize(limitBytes)}).")
            : used;
    }
}
