using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Locks;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>How 'sandbox exec' wraps its command in the platform's scope: systemd-run on Linux, a process group on a Mac (#1936).</summary>
internal static class ExecScope
{
    // $ARGV[0] is the group id file, the rest the command. Unlike a Step (tmux makes its first process a session leader), the exec's command is a plain child of sindri, so this makes it its own process group and,
    // when sindri owns the terminal, that group the terminal's foreground group (no setsid: it would drop the controlling tty; a background group would stop on a stdin read and miss Ctrl-C).
    internal const string MacScript =
        "use POSIX (); use File::Basename (); use File::Path ();\n" +
        "my $file = shift @ARGV; my $old = POSIX::getpgrp();\n" +
        "POSIX::setpgid(0, 0) or $old == $$ or do { print STDERR \"sindri: could not make the command its own process group: $!\\n\"; exit 70 };\n" +
        "my $group = POSIX::getpgrp();\n" +
        "File::Path::make_path(File::Basename::dirname($file));\n" +
        "my $ok = open(my $out, '>', \"$file.tmp\"); $ok &&= print($out \"$group\\n\"); $ok &&= close($out); $ok &&= rename(\"$file.tmp\", $file);\n" +
        "$ok or do { print STDERR \"sindri: could not write $file: $!\\n\"; exit 70 };\n" +
        "if (open(my $tty, '+<', '/dev/tty')) { my $fd = fileno($tty); if (POSIX::tcgetpgrp($fd) == $old) { local $SIG{TTOU} = 'IGNORE'; POSIX::tcsetpgrp($fd, $group); } close($tty); }\n" +
        "exec { $ARGV[0] } @ARGV or do { print STDERR \"sindri: could not start $ARGV[0]: $!\\n\"; exit 127 };";

    /// <summary>Gets the Linux scope: <see cref="SystemdScopeArgv.Start"/>.</summary>
    /// <param name="systemdRun">The systemd-run binary.</param>
    /// <returns>The scope function.</returns>
    internal static Func<LaunchId, ScopeLimits, IReadOnlyList<string>, IReadOnlyList<string>> Linux(string systemdRun) =>
        (launch, limits, inner) => SystemdScopeArgv.Start(systemdRun, launch, limits, inner);

    /// <summary>Gets the macOS scope: a perl wrapper that makes the command its own process group.</summary>
    /// <param name="pgroupsDirectory">The folder of the group files.</param>
    /// <returns>The scope function.</returns>
    internal static Func<LaunchId, ScopeLimits, IReadOnlyList<string>, IReadOnlyList<string>> Mac(string pgroupsDirectory) =>
        (launch, _, inner) =>
        ["/usr/bin/perl", "-e", MacScript, ProcessGroupFiles.PgidPath(pgroupsDirectory, launch.Value) ?? throw new ArgumentException("the launch is not a file name", nameof(launch)), .. inner];

    /// <summary>Gets what the macOS exec does after its command ends: ends the command's group if it is still alive, then removes the group file (the .oom record stays) and gives the terminal back to sindri if the command's group still holds it.</summary>
    /// <param name="pgroupsDirectory">The folder of the group files.</param>
    /// <returns>The action.</returns>
    internal static Action<LaunchId> MacEnded(string pgroupsDirectory) => launch =>
    {
        var group = ProcessGroupFiles.ReadPgid(pgroupsDirectory, launch.Value);
        if (group is { } live && ProcessGroupFiles.Alive(live))
        {
            // why: on a Mac nothing ties the command to sindri (unlike bwrap on Linux), so a cancelled exec's group keeps running; without its file the cap stops counting it and the watchdog can't signal it
            _ = ProcessGroupFiles.Signal(live, "SIGKILL");
        }

        ProcessGroupFiles.Remove(pgroupsDirectory, launch.Value);
        if (group is { } pgid)
        {
            Libc.TakeForeground(pgid);   // why: the command's group was the terminal's foreground group; sindri is now in the background and would stop on its next terminal read
        }
    };
}
