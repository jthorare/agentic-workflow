using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>
/// Everything the macOS adapters ask the host: memory (sysctl, vm_stat), swap, the process list and one process's age (ps), a
/// SIGTERM (kill) and the state disk. One place runs the tools, so the host resources, the process table and the liveness check
/// share the same calls, error wrapping (AW-META-010 "unavailable on macOS: ...") and parsing.
/// </summary>
/// <param name="stateDirectory">A directory on the disk whose free space is reported.</param>
/// <param name="runner">Runs the tools.</param>
/// <param name="paths">Where the tools are.</param>
/// <param name="diskFree">Free bytes of a directory (DiskSpace.Free in the host).</param>
internal sealed class MacHostReader(string stateDirectory, ICommandRunner runner, MacToolPaths paths, Func<string, long> diskFree)
{
    /// <summary>The total memory in kilobytes ("sysctl -n hw.memsize").</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Kilobytes, or AW-META-010.</returns>
    internal async ValueTask<Result<long>> TotalMemoryKb(CancellationToken cancellationToken)
    {
        var total = await MacTools.Output(runner, [paths.Sysctl, "-n", "hw.memsize"], cancellationToken).ConfigureAwait(false);
        return total.IsError ? total.Error : Parsed(() => MacFormat.MemTotalKb(total.Value));
    }

    /// <summary>Available memory (vm_stat), swap (sysctl vm.swapusage), in that order, and the state disk.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The figures, or the first tool's AW-META-010.</returns>
    internal async ValueTask<Result<MacMemory>> Memory(CancellationToken cancellationToken)
    {
        var vm = await MacTools.Output(runner, [paths.VmStat], cancellationToken).ConfigureAwait(false);
        var swap = await MacTools.Output(runner, [paths.Sysctl, "-n", "vm.swapusage"], cancellationToken).ConfigureAwait(false);
        if (vm.IsError || swap.IsError)
        {
            return vm.IsError ? vm.Error : swap.Error;
        }

        return Parsed(() =>
        {
            var (swapTotal, swapUsed) = MacFormat.SwapUsage(swap.Value);
            return new MacMemory(MacFormat.AvailableKb(vm.Value), swapTotal, swapUsed, diskFree(stateDirectory));
        });
    }

    /// <summary>Every live process ("ps -axww").</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The processes, or AW-META-010.</returns>
    internal async ValueTask<Result<IReadOnlyList<ProcessInfo>>> Processes(CancellationToken cancellationToken)
    {
        var ps = await MacTools.Output(runner, [paths.Ps, "-axww", "-o", "pid=,ppid=,etime=,rss=,stat=,args="], cancellationToken).ConfigureAwait(false);
        return ps.IsError ? ps.Error : Parsed(() => MacFormat.Processes(ps.Value));
    }

    /// <summary>The working directory and open files of every process this user can inspect ("lsof -F pcnR": pid, parent, command, names).</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>lsof's field output, or AW-META-010.</returns>
    internal ValueTask<Result<string>> Lsof(CancellationToken cancellationToken) =>
        MacTools.Output(runner, [paths.Lsof, "-w", "-n", "-F", "pcnR"], cancellationToken, exit1WithOutputIsUsable: true);

    /// <summary>How long every live process has run, from one ps ("ps -axo pid=,etime=").</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Each listed pid's age in seconds, or AW-META-010.</returns>
    internal async ValueTask<Result<IReadOnlyDictionary<int, long>>> ProcessAges(CancellationToken cancellationToken)
    {
        var ps = await MacTools.Output(runner, [paths.Ps, "-axo", "pid=,etime="], cancellationToken).ConfigureAwait(false);
        return ps.IsError ? ps.Error : Parsed(() => MacFormat.PidAges(ps.Value));
    }

    /// <summary>How long a process has run ("ps -o etime= -p pid"), or null when there is no such process.</summary>
    /// <param name="pid">The process id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The age, null for no process, or AW-META-010.</returns>
    internal async ValueTask<Result<TimeSpan?>> ProcessAge(int pid, CancellationToken cancellationToken)
    {
        var argv = new[] { paths.Ps, "-o", "etime=", "-p", pid.ToString(CultureInfo.InvariantCulture) };
        var ran = await runner.Run(new Core.Commands.CommandRequest(argv, TimeSpan.FromSeconds(15)), cancellationToken).ConfigureAwait(false);
        if (ran.IsError)
        {
            return MetaErrors.HostUnreadable.With($"unavailable on macOS: ps could not be run ({MacTools.Clip(ran.Error.Message)})");
        }

        if (ran.Value.TimedOut)
        {
            return MetaErrors.HostUnreadable.With("unavailable on macOS: ps timed out");
        }

        // ps -p exits 1 and prints nothing for a pid that is not running; any other failure is the tool's, not an answer
        var text = ran.Value.Stdout.Trim();
        if (ran.Value.ExitCode > 1 || (ran.Value.ExitCode == 1 && text.Length > 0))
        {
            return MetaErrors.HostUnreadable.With(string.Create(CultureInfo.InvariantCulture, $"unavailable on macOS: {paths.Ps} exited {ran.Value.ExitCode}: {MacTools.Clip(ran.Value.Stderr.Trim())}"));
        }

        if (text.Length == 0)
        {
            TimeSpan? none = null;
            return Results.Success(none);
        }

        return MacFormat.Elapsed(text) is { } seconds
            ? Results.Success<TimeSpan?>(TimeSpan.FromSeconds(seconds))
            : MetaErrors.HostUnreadable.With("unavailable on macOS: ps reported an elapsed time that can't be read: " + text);
    }

    /// <summary>Sends SIGTERM ("kill -TERM pid").</summary>
    /// <param name="pid">The process id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or AW-META-010.</returns>
    internal async ValueTask<Result<Unit>> Terminate(int pid, CancellationToken cancellationToken)
    {
        var killed = await MacTools.Output(runner, [paths.Kill, "-TERM", pid.ToString(CultureInfo.InvariantCulture)], cancellationToken).ConfigureAwait(false);
        return killed.IsError ? killed.Error : Results.Ok();
    }

    private static Result<T> Parsed<T>(Func<T> parse)
    {
        try
        {
            return parse();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException or OverflowException)
        {
            return MetaErrors.HostUnreadable.With("unavailable on macOS: " + exception.Message);
        }
    }
}
