using System.Diagnostics;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Desktop;

namespace Aw.Sindri.Ui.Tests.Desktop;

/// <summary>
/// #3644 and #3645: the REAL detached start, run on a short sleep, leaves the child safe from the app's terminal closing, with or without perl. With the setsid program on the PATH the child is
/// in a new session; with neither setsid nor perl it ignores SIGHUP, which is what a closing terminal sends. Linux only: both are read from /proc.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class DetachedProcessStarterSessionTests : IDisposable
{
    private const string NoPerl = "/nonexistent/perl";

    private readonly string _dir = Directory.CreateTempSubdirectory("detach-test-").FullName;
    private int _pid;

    // A PATH that holds only the named tools (and sleep, the child), so perl, and setsid when asked, are hidden from the shell.
    private string PathWith(params string[] tools)
    {
        foreach (var tool in tools.Append("sleep"))
        {
            var real = new[] { "/usr/bin", "/bin" }.Select(d => Path.Combine(d, tool)).FirstOrDefault(File.Exists);
            if (real is not null)
            {
                File.CreateSymbolicLink(Path.Combine(_dir, tool), real);
            }
        }

        return _dir;
    }

    private string StartSleep(string path)
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "reads /proc");
        var starter = new DetachedProcessStarter(info =>
        {
            info.Environment["PATH"] = path;
            var process = Process.Start(info);
            _pid = process?.Id ?? 0;
            return process;
        }, NoPerl);

        var started = starter.Start(new ProcessLaunch("sleep", ["30"], Path.Combine(_dir, "logs", "serve.log"), Number: 1));

        Assert.True(started.IsSuccess);
        WaitUntilSleeping();
        return $"/proc/{_pid}";
    }

    // The shell execs the child, so the pid is the sleep's own once the exec has happened.
    private void WaitUntilSleeping()
    {
        var until = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < until && !(File.Exists($"/proc/{_pid}/comm") && File.ReadAllText($"/proc/{_pid}/comm").Trim() == "sleep"))
        {
            Thread.Sleep(20);
        }

        Assert.Equal("sleep", File.ReadAllText($"/proc/{_pid}/comm").Trim());
    }

    private static int SessionOf(string proc)
    {
        var stat = File.ReadAllText(proc + "/stat");
        return int.Parse(stat[(stat.LastIndexOf(')') + 2)..].Split(' ')[3], System.Globalization.CultureInfo.InvariantCulture);   // state, ppid, pgrp, session
    }

    [Fact]
    public void With_setsid_on_the_PATH_and_no_perl_the_child_is_in_a_new_session()
    {
        var proc = StartSleep(PathWith("setsid"));

        Assert.NotEqual(SessionOf("/proc/self"), SessionOf(proc));
    }

    [Fact]
    public void With_neither_setsid_nor_perl_the_child_ignores_SIGHUP_so_a_closing_terminal_does_not_kill_it()
    {
        var proc = StartSleep(PathWith());

        var ignored = File.ReadAllLines(proc + "/status").Single(l => l.StartsWith("SigIgn:", StringComparison.Ordinal))["SigIgn:".Length..].Trim();
        Assert.True((Convert.ToUInt64(ignored, 16) & 1) == 1, $"SIGHUP is not ignored (SigIgn {ignored})");
    }

    public void Dispose()
    {
        if (_pid > 0)
        {
            try
            {
                Process.GetProcessById(_pid).Kill();   // by the recorded pid, never by name
            }
            catch (ArgumentException)
            {
                // already gone
            }
            catch (InvalidOperationException)
            {
            }
        }

        Directory.Delete(_dir, recursive: true);
    }
}
