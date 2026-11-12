using System.Diagnostics;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Control;

/// <summary>
/// Starts the restart's helper (#3052): '/bin/sh' waits for this serve's pid to exit (60 seconds at most), then execs 'bin/cs/current/sindri serve', output appended to serve.log.
/// It runs in its own session (perl's POSIX::setsid, as <see cref="ServeLauncher"/> does), so it outlives the serve that started it. Without perl it is a plain child: it still outlives
/// serve, which does not signal its children, but a terminal's hang-up would reach it.
/// </summary>
/// <param name="settings">Where the logs go.</param>
/// <param name="baseDirectory">The folder this process runs from.</param>
internal sealed class ServeRelauncher(SindriSettings settings, string baseDirectory) : IServeRelauncher
{
    private const string Perl = "/usr/bin/perl";

    public Result<Unit> Schedule()
    {
        var logs = Path.Combine(settings.Home, "logs");
        OwnerOnly.EnsureDirectory(settings.Home);
        OwnerOnly.EnsureDirectory(logs);
        var log = Path.Combine(logs, "serve.log");
        if (RelaunchCommand.Argv(baseDirectory, Environment.ProcessId, log) is not { } argv)
        {
            return SindriErrors.BuildRelaunchFailed.With("this process doesn't run from bin/cs/<stamp>, so there is no installed build to start");
        }

        var detached = File.Exists(Perl)
            ? new[] { Perl, "-MPOSIX", "-e", "POSIX::setsid() == -1 and die \"setsid: $!\\n\"; exec @ARGV or die \"exec $ARGV[0]: $!\\n\"", "--" }.Concat(argv).ToList()
            : argv.ToList();
        try
        {
            using var process = Process.Start(ChildEnvironment.Start(detached[0], detached.Skip(1)));
            return process is null
                ? SindriErrors.BuildRelaunchFailed.With("the relauncher didn't start")
                : Results.Ok();
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return SindriErrors.BuildRelaunchFailed.With($"the relauncher didn't start ({e.Message})");
        }
    }
}
