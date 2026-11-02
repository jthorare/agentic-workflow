using System.Globalization;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>ICgroupStats over the cgroup v2 files under the cgroup root. A file that is not there (the scope ended) is a gone scope, never an exception.</summary>
/// <param name="root">The cgroup root.</param>
internal sealed class CgroupFiles(string root = "/sys/fs/cgroup") : ICgroupStats
{
    /// <inheritdoc />
    public CgroupSample? Read(string cgroupPath)
    {
        var dir = Path.Combine(root, cgroupPath.TrimStart('/'));
        try
        {
            var events = File.ReadAllText(Path.Combine(dir, "memory.events"));   // the file that goes with the scope: its absence is the answer
            return new CgroupSample(
                OomKills: Keyed(events, "oom_kill") ?? 0,
                MemoryCurrent: Single(dir, "memory.current"),
                MemoryPeak: Single(dir, "memory.peak"),
                Pids: (int)Single(dir, "pids.current"),
                CpuMicroseconds: Keyed(Text(dir, "cpu.stat"), "usage_usec") ?? 0,
                PidsMax: Keyed(Text(dir, "pids.events"), "max") ?? 0,
                PidsPeak: Single(dir, "pids.peak"));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string Text(string dir, string name)
    {
        try
        {
            return File.ReadAllText(Path.Combine(dir, name));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return string.Empty;   // a controller the scope lacks (memory.peak before Linux 5.19) reads as zero
        }
    }

    private static long Single(string dir, string name) =>
        long.TryParse(Text(dir, name).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static long? Keyed(string text, string key)
    {
        foreach (var line in text.Split('\n'))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts is [var name, var number] && string.Equals(name, key, StringComparison.Ordinal) && long.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
        }

        return null;
    }
}
