using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>
/// IProcessLiveness on Linux: the process's start time from /proc/&lt;pid&gt;/stat (clock ticks since boot, 100 a second) and
/// /proc/uptime, checked against when the file naming the pid was written. A /proc entry that vanishes mid-read is a process that exited.
/// </summary>
/// <param name="time">The clock the start time is computed from.</param>
/// <param name="read">Reads a /proc file as text (File.ReadAllTextAsync in the host).</param>
internal sealed class ProcProcessLiveness(TimeProvider time, Func<string, CancellationToken, Task<string>> read) : IProcessLiveness
{
    private const double ClockTicksPerSecond = 100;

    public ProcProcessLiveness(TimeProvider time)
        : this(time, File.ReadAllTextAsync)
    {
    }

    public async ValueTask<Result<bool>> IsRunning(int pid, DateTimeOffset recordedAt, CancellationToken cancellationToken)
    {
        if (pid <= 0)
        {
            return false;
        }

        string stat;
        try
        {
            stat = await read("/proc/" + pid.ToString(CultureInfo.InvariantCulture) + "/stat", cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return false;   // no /proc entry: the process exited
        }
        catch (UnauthorizedAccessException exception)
        {
            return MetaErrors.HostUnreadable.With("/proc could not be read: " + exception.Message);
        }

        try
        {
            if (ProcFormat.Stat(stat) is not { } fields)
            {
                return false;
            }

            // An unreadable uptime is not an answer about the process, and an uptime shorter than the process's start (the two
            // clocks disagree) can't give an age: both are AW-META-010, never "not running" or a reused pid.
            var uptime = double.Parse((await read("/proc/uptime", cancellationToken).ConfigureAwait(false)).Split(' ')[0], CultureInfo.InvariantCulture);
            var started = fields.StartTicks / ClockTicksPerSecond;
            return uptime < started
                ? MetaErrors.HostUnreadable.With(string.Create(CultureInfo.InvariantCulture, $"/proc is inconsistent: uptime {uptime}s is before pid {pid}'s start at {started}s"))
                : !ProcessStart.IsReused(time.GetUtcNow() - TimeSpan.FromSeconds(uptime - started), recordedAt);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException or OverflowException)
        {
            return MetaErrors.HostUnreadable.With("/proc could not be read: " + exception.Message);
        }
    }

    public async ValueTask<IReadOnlyList<Result<bool>>> Running(IReadOnlyList<ProcessClaim> claims, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claims);
        var answers = new List<Result<bool>>(claims.Count);
        foreach (var claim in claims)
        {
            answers.Add(await IsRunning(claim.Pid, claim.RecordedAt, cancellationToken).ConfigureAwait(false));
        }

        return answers;
    }
}
