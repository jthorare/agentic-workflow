using System.ComponentModel;
using System.Diagnostics;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Infrastructure.Lease;

/// <summary>
/// The process probe on macOS and Linux (design spec §9.1): pids and start times from the OS, the boot session from
/// sysctl kern.bootsessionuuid (macOS) or /proc/sys/kernel/random/boot_id (Linux). Start times are whole seconds.
/// </summary>
internal sealed class PosixProcessProbe : IProcessProbe
{
    private readonly Lazy<LeaseHolder> _self = new(ReadSelf);

    public LeaseHolder Self() => _self.Value;

    public ProcessFacts Facts(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return new ProcessFacts(Running: !process.HasExited, StartTime(process));
        }
        catch (ArgumentException)
        {
            return new ProcessFacts(Running: false, StartedAt: null);   // no process with that id
        }
        catch (InvalidOperationException)
        {
            return new ProcessFacts(Running: false, StartedAt: null);   // it exited while being read
        }
    }

    private static LeaseHolder ReadSelf()
    {
        using var self = Process.GetCurrentProcess();
        return new LeaseHolder(HostId.From(Environment.MachineName), Environment.ProcessId,
            StartTime(self) ?? throw new InvalidOperationException("This process's start time is unreadable."), BootId.From(ReadBootId()));
    }

    private static DateTimeOffset? StartTime(Process process)
    {
        try
        {
            var utc = process.StartTime.ToUniversalTime();
            return new DateTimeOffset(utc.Ticks - (utc.Ticks % TimeSpan.TicksPerSecond), TimeSpan.Zero);
        }
        catch (Win32Exception)
        {
            return null;   // another user's process: it runs, but its start time can't be read
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static string ReadBootId()
    {
        if (OperatingSystem.IsLinux())
        {
            return File.ReadAllText("/proc/sys/kernel/random/boot_id").Trim();
        }

        using var sysctl = Process.Start(new ProcessStartInfo("/usr/sbin/sysctl", ["-n", "kern.bootsessionuuid"])
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
        }) ?? throw new InvalidOperationException("sysctl didn't start");
        var id = sysctl.StandardOutput.ReadToEnd().Trim();
        sysctl.WaitForExit();
        return id.Length > 0 ? id : throw new InvalidOperationException("sysctl kern.bootsessionuuid printed nothing");
    }
}
