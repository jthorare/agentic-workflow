using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>
/// IProcessTable over /proc. Zombies (state Z) are left out: they hold no memory and a terminated child of this process
/// stays one until it is reaped. Termination is SIGTERM by pid, through the kill program.
/// </summary>
/// <param name="stateDirectory">A directory on the disk whose free space is reported.</param>
internal sealed class ProcProcessTable(string stateDirectory) : IProcessTable
{
    private const double ClockTicksPerSecond = 100;
    private const long BytesPerKb = 1024;

    public async ValueTask<Result<HostSnapshot>> Snapshot(CancellationToken cancellationToken)
    {
        try
        {
            var uptime = double.Parse((await File.ReadAllTextAsync("/proc/uptime", cancellationToken).ConfigureAwait(false)).Split(' ')[0], CultureInfo.InvariantCulture);
            var processes = new List<ProcessInfo>();
            foreach (var directory in Directory.EnumerateDirectories("/proc").Where(d => int.TryParse(Path.GetFileName(d), NumberStyles.None, CultureInfo.InvariantCulture, out _)))
            {
                if (await ReadProcess(directory, uptime, cancellationToken).ConfigureAwait(false) is { } process)
                {
                    processes.Add(process);
                }
            }

            var meminfo = ProcFormat.MemInfo(await File.ReadAllTextAsync("/proc/meminfo", cancellationToken).ConfigureAwait(false));
            return new HostSnapshot(processes, meminfo.Available, meminfo.SwapUsed, DiskSpace.Free(stateDirectory));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException or ArgumentException)
        {
            return MetaErrors.HostUnreadable.With(exception.Message);
        }
    }

    public ValueTask<Result<Unit>> Terminate(int pid, CancellationToken cancellationToken) => SignalProcess.Terminate(pid, cancellationToken);

    // A process can exit between listing and reading: its files then vanish, and it is simply not listed.
    private static async ValueTask<ProcessInfo?> ReadProcess(string directory, double uptime, CancellationToken cancellationToken)
    {
        try
        {
            var stat = await File.ReadAllTextAsync(Path.Combine(directory, "stat"), cancellationToken).ConfigureAwait(false);
            var fields = ProcFormat.Stat(stat);
            if (fields is null || fields.State == 'Z')
            {
                return null;
            }

            var raw = await File.ReadAllTextAsync(Path.Combine(directory, "cmdline"), cancellationToken).ConfigureAwait(false);
            var cmdline = raw.Replace('\0', ' ').Trim();
            return new ProcessInfo(
                int.Parse(Path.GetFileName(directory), CultureInfo.InvariantCulture),
                fields.Ppid,
                cmdline.Length > 0 ? cmdline : "[" + fields.Name + "]",
                fields.RssPages * Environment.SystemPageSize / BytesPerKb,
                (long)Math.Max(0, uptime - (fields.StartTicks / ClockTicksPerSecond)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
