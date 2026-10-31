using System.Collections.Concurrent;
using System.Globalization;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// ICgroupStats over a process group (macOS, SB-9b): one 'ps -A -o pid=,pgid=,rss=,time=' run (the pid list, the count and the CPU), filtered to the group in code. That form means the same on macOS and
/// on Linux procps, which 'ps -g' does not. The group's memory is the system's phys footprint of its processes when <c lang="text">footprint</c> answers, else the sum of their resident sets, which counts shared pages once per process: a high figure,
/// which is the safe side of a kill line.
/// </summary>
/// <param name="processes">Runs ps.</param>
/// <param name="ps">The ps program.</param>
/// <param name="footprint">The group's memory as the system charges it; null, or a null answer, leaves the sum of resident sets.</param>
internal sealed class ProcessGroupStats(ISandboxProcesses processes, string ps = "ps", IPhysFootprint? footprint = null) : ICgroupStats
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);
    private readonly ConcurrentDictionary<string, long> _peaks = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public CgroupSample? Read(string cgroupPath)
    {
        ArgumentNullException.ThrowIfNull(cgroupPath);
        if (!cgroupPath.StartsWith(ProcessGroupScopeControl.Prefix, StringComparison.Ordinal)
            || !int.TryParse(cgroupPath.AsSpan(ProcessGroupScopeControl.Prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var pgid))
        {
            return null;
        }

        var run = processes.Run(ps, ["-A", "-o", "pid=,pgid=,rss=,time="], _timeout);
        if (!run.Succeeded)
        {
            return null;
        }

        var members = run.Output.Split('\n').Select(Member).OfType<Row>().Where(m => m.Group == pgid).ToList();
        if (members.Count == 0)
        {
            return null;
        }

        var memory = footprint?.Sum([.. members.Select(m => m.Pid)]) ?? members.Sum(m => m.RssBytes);   // why: summed resident sets count a shared page once per process (8 MSBuild nodes read ~2.0 GB where the footprint is ~1.4 GB)
        var peak = _peaks.AddOrUpdate(cgroupPath, static (_, now) => now, static (_, before, now) => Math.Max(before, now), memory);
        return new CgroupSample(
            OomKills: 0,   // why: the memory-kill record is keyed by unit and read through IScopeControl.Result, which the watchdog asks at its verdict
            MemoryCurrent: memory,
            MemoryPeak: peak,
            Pids: members.Count,
            CpuMicroseconds: members.Sum(m => m.CpuMicroseconds));
    }

    /// <summary>Reads a ps CPU time, '[[dd-]hh:]mm:ss[.ff]': macOS prints 'm:ss.ff', procps 'hh:mm:ss' and 'dd-hh:mm:ss'.</summary>
    /// <param name="text">The time.</param>
    /// <returns>Microseconds, or null when it is not a time.</returns>
    internal static long? CpuMicroseconds(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var days = 0L;
        var rest = text;
        if (text.IndexOf('-', StringComparison.Ordinal) is var dash and > 0)
        {
            if (!long.TryParse(text.AsSpan(0, dash), NumberStyles.None, CultureInfo.InvariantCulture, out days))
            {
                return null;
            }

            rest = text[(dash + 1)..];
        }

        var parts = rest.Split(':');
        if (parts.Length is < 2 or > 3
            || !double.TryParse(parts[^1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var seconds)
            || !long.TryParse(parts[^2], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes))
        {
            return null;
        }

        var hours = 0L;
        if (parts.Length == 3 && !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out hours))
        {
            return null;
        }

        return (((((days * 24) + hours) * 60) + minutes) * 60 * 1_000_000) + (long)Math.Round(seconds * 1_000_000, MidpointRounding.AwayFromZero);
    }

    private static Row? Member(string line)
    {
        var fields = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return fields.Length == 4
            && int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var group)
            && int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var pid)
            && long.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out var rss)
            && CpuMicroseconds(fields[3]) is { } cpu
                ? new Row(pid, group, rss * 1024, cpu)
                : null;
    }

    private sealed record Row(int Pid, int Group, long RssBytes, long CpuMicroseconds);
}

