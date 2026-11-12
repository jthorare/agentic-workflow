using System.Diagnostics;
using Aw.Kernel;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>
/// Starts a program detached from the app, its output appended to the log (first-run spec decision 2). On Linux and macOS it
/// runs through /bin/sh with exec under umask 077, stdin from /dev/null and stdout and stderr appended to the log, in its own
/// session (perl's POSIX::setsid before the exec) so closing the app doesn't signal it; without /usr/bin/perl it is a plain exec.
/// </summary>
internal sealed class DetachedProcessStarter : IProcessStarter
{
    private const string Perl = "/usr/bin/perl";
    private const string FailureCode = "SND-CLI-002";

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

            var detach = File.Exists(Perl)
                ? $"exec {Perl} -MPOSIX -e 'POSIX::setsid() == -1 and die \"{FailureCode} setsid: $!\\n\"; exec @ARGV or die \"{FailureCode} exec $ARGV[0]: $!\\n\"'"
                : "exec";
            var info = new ProcessStartInfo("/bin/sh")
            {
                UseShellExecute = false,
                RedirectStandardInput = false,
            };
            info.ArgumentList.Add("-c");
            info.ArgumentList.Add($"umask 077; {detach} \"$0\" \"$@\" </dev/null >>\"$LOG\" 2>&1");
            info.ArgumentList.Add(launch.Executable);
            foreach (var argument in launch.Arguments)
            {
                info.ArgumentList.Add(argument);
            }

            info.Environment["LOG"] = launch.LogPath;
            using var process = Process.Start(info);
            return process is null ? Fail("/bin/sh didn't start") : Unit.Value;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return Fail(e.Message);
        }
    }

    private static Error Fail(string reason) => new(FailureCode, ErrorKind.Unavailable, reason);
}
