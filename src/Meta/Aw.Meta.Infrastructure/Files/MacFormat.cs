using System.Globalization;
using Aw.Meta.Core.Health;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>Parses the text of the macOS tools the macOS host adapters run: sysctl, vm_stat and ps.</summary>
internal static class MacFormat
{
    private const long BytesPerKb = 1024;
    private const string PageSizeMarker = "page size of ";
    private const int PsColumns = 5;

    /// <summary>The total memory in kilobytes, from "sysctl -n hw.memsize" (bytes).</summary>
    /// <param name="sysctl">The tool's output.</param>
    /// <returns>Kilobytes.</returns>
    internal static long MemTotalKb(string sysctl) =>
        long.Parse(sysctl.Trim(), NumberStyles.None, CultureInfo.InvariantCulture) / BytesPerKb;

    /// <summary>The available memory in kilobytes, from vm_stat: free, inactive and speculative pages times the page size.</summary>
    /// <param name="vmStat">The tool's output.</param>
    /// <returns>Kilobytes.</returns>
    internal static long AvailableKb(string vmStat)
    {
        var marker = vmStat.IndexOf(PageSizeMarker, StringComparison.Ordinal);
        if (marker < 0)
        {
            throw new FormatException("vm_stat reported no page size");
        }

        var pageSize = Digits(vmStat.AsSpan(marker + PageSizeMarker.Length));
        return (Pages(vmStat, "Pages free") + Pages(vmStat, "Pages inactive") + Pages(vmStat, "Pages speculative")) * pageSize / BytesPerKb;
    }

    /// <summary>The swap total and used, in kilobytes, from "sysctl -n vm.swapusage".</summary>
    /// <param name="swapUsage">The tool's output.</param>
    /// <returns>Total and used kilobytes.</returns>
    internal static (long TotalKb, long UsedKb) SwapUsage(string swapUsage) => (Amount(swapUsage, "total"), Amount(swapUsage, "used"));

    /// <summary>The processes of "ps -axww -o pid=,ppid=,etime=,rss=,stat=,args=". Zombies and lines that are not processes are left out.</summary>
    /// <param name="ps">The tool's output.</param>
    /// <returns>The live processes.</returns>
    internal static IReadOnlyList<ProcessInfo> Processes(string ps)
    {
        List<ProcessInfo> processes = [];
        foreach (var line in ps.ReplaceLineEndings("\n").Split('\n'))
        {
            if (Process(line) is { } process)
            {
                processes.Add(process);
            }
        }

        return processes;
    }

    private static ProcessInfo? Process(string line)
    {
        var columns = line.Split(' ', PsColumns + 1, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (columns.Length < PsColumns || columns[4][0] == 'Z'
            || !int.TryParse(columns[0], NumberStyles.None, CultureInfo.InvariantCulture, out var pid)
            || !int.TryParse(columns[1], NumberStyles.None, CultureInfo.InvariantCulture, out var ppid)
            || !long.TryParse(columns[3], NumberStyles.None, CultureInfo.InvariantCulture, out var rss)
            || ElapsedSeconds(columns[2]) is not { } age)
        {
            return null;
        }

        var args = columns.Length > PsColumns ? columns[PsColumns] : string.Empty;
        return new ProcessInfo(pid, ppid, args.Length > 0 ? args : "[" + pid.ToString(CultureInfo.InvariantCulture) + "]", rss, age);
    }

    // ps etime: [[dd-]hh:]mm:ss
    private static long? ElapsedSeconds(string etime)
    {
        var days = 0L;
        var clock = etime;
        var dash = etime.IndexOf('-', StringComparison.Ordinal);
        if (dash >= 0)
        {
            if (!long.TryParse(etime.AsSpan(0, dash), NumberStyles.None, CultureInfo.InvariantCulture, out days))
            {
                return null;
            }

            clock = etime[(dash + 1)..];
        }

        var seconds = 0L;
        foreach (var part in clock.Split(':'))
        {
            if (!long.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            {
                return null;
            }

            seconds = (seconds * 60) + value;
        }

        return (days * 86_400) + seconds;
    }

    private static long Pages(string vmStat, string key)
    {
        var start = vmStat.IndexOf(key + ":", StringComparison.Ordinal);
        return start < 0
            ? throw new FormatException("vm_stat has no \"" + key + "\" line")
            : Digits(vmStat.AsSpan(start + key.Length + 1).TrimStart());
    }

    private static long Digits(ReadOnlySpan<char> text)
    {
        var length = 0;
        while (length < text.Length && char.IsAsciiDigit(text[length]))
        {
            length++;
        }

        return length == 0 ? throw new FormatException("a number was expected") : long.Parse(text[..length], NumberStyles.None, CultureInfo.InvariantCulture);
    }

    // "total = 5632.00M" (the decimal separator follows the locale: "2,00G" is two gigabytes).
    private static long Amount(string swapUsage, string key)
    {
        var start = swapUsage.IndexOf(key + " =", StringComparison.Ordinal);
        if (start < 0)
        {
            throw new FormatException("vm.swapusage has no \"" + key + "\" figure");
        }

        var text = swapUsage.AsSpan(start + key.Length + 2).TrimStart();
        var length = 0;
        while (length < text.Length && (char.IsAsciiDigit(text[length]) || text[length] is '.' or ','))
        {
            length++;
        }

        var unit = length < text.Length ? text[length] : 'K';
        var number = double.Parse(text[..length].ToString().Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        var factor = unit switch { 'G' => BytesPerKb * BytesPerKb, 'M' => BytesPerKb, 'K' => 1, _ => throw new FormatException("unknown swap unit " + unit) };
        return (long)Math.Round(number * factor, MidpointRounding.AwayFromZero);
    }
}
