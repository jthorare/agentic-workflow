using System.Diagnostics;
using Aw.Kernel;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>
/// Starts a program detached from the app, its output appended to the log (first-run spec decision 2). On Linux and macOS it
/// runs through /bin/sh with exec under umask 077, stdin from /dev/null and stdout and stderr appended to the log, in its own
/// session (perl's POSIX::setsid before the exec) so closing the app doesn't signal it; without /usr/bin/perl it is a plain exec.
/// </summary>
/// <param name="spawn">Starts the process (Process.Start unless a test swaps it).</param>
internal sealed class DetachedProcessStarter(Func<ProcessStartInfo, Process?>? spawn = null) : IProcessStarter
{
    private readonly Func<ProcessStartInfo, Process?> _spawn = spawn ?? (static info => Process.Start(info));

    private const string Perl = "/usr/bin/perl";
    private const string FailureCode = "SND-CLI-002";

    public event EventHandler<SindriExitedEventArgs>? Exited;

    public Result<Unit> Start(ProcessLaunch launch)
    {
        ArgumentNullException.ThrowIfNull(launch);
        if (OperatingSystem.IsWindows())
        {
            return Fail("starting Sindri detached isn't supported on Windows yet");
        }

        try
        {
            if (Path.GetDirectoryName(launch.LogPath) is { Length: > 0 } logs)
            {
                _ = Directory.CreateDirectory(logs);
            }

            var info = Info(launch);
#pragma warning disable CA2000 // the Exited handler disposes the process once it has ended
            var process = _spawn(info);
#pragma warning restore CA2000
            if (process is null)
            {
                return Fail("/bin/sh didn't start");
            }

            Watch(process, launch);
            return Unit.Value;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return Fail(e.Message);
        }
    }

    internal static ProcessStartInfo Info(ProcessLaunch launch)
    {
        var detach = File.Exists(Perl)
            ? $"exec {Perl} -MPOSIX -e 'POSIX::setsid() == -1 and die \"{FailureCode} setsid: $!\\n\"; exec @ARGV or die \"{FailureCode} exec $ARGV[0]: $!\\n\"'"
            : "exec";
        var info = new ProcessStartInfo("/bin/sh")
        {
            UseShellExecute = false,
            RedirectStandardInput = false,
        };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add($"umask 077; {detach} \"$0\" \"$@\" </dev/null >>\"$AW_START_LOG\" 2>&1");
        info.ArgumentList.Add(launch.Executable);
        foreach (var argument in launch.Arguments)
        {
            info.ArgumentList.Add(argument);
        }

        info.Environment["AW_START_LOG"] = launch.LogPath;

        return Scrub(info);
    }

    /// <summary>Takes out of the child's environment what the app's own driver left (#3643): serve must not see AW_UI_DRIVER*.</summary>
    /// <param name="info">The start info.</param>
    /// <returns>The same start info.</returns>
    internal static ProcessStartInfo Scrub(ProcessStartInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        foreach (var key in info.Environment.Keys.Where(static k => k.StartsWith("AW_UI_DRIVER", StringComparison.Ordinal)).ToList())
        {
            _ = info.Environment.Remove(key);
        }

        return info;
    }

    // The shell execs the program, so the pid is the program's own: its exit is the program's. The handler is set before
    // EnableRaisingEvents, which raises it at once for a process that already ended.
    private void Watch(Process process, ProcessLaunch launch)
    {
        process.Exited += (_, _) =>
        {
            var code = process.ExitCode;
            process.Dispose();
            Exited?.Invoke(this, new SindriExitedEventArgs(launch.Number, code));
        };
        process.EnableRaisingEvents = true;
    }

    private static Error Fail(string reason) => new(FailureCode, ErrorKind.Unavailable, reason);
}
