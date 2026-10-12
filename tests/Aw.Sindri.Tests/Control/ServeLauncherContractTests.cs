using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Control;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Control;

/// <summary>
/// The real launcher over a stub executable (a shell script standing in for 'sindri'), plus what only the real binding
/// has: the log's permissions, the child's own session, and perl's error line when the exec fails.
/// </summary>
[Lane(TestLane.Integration)]
[UnsupportedOSPlatform("windows")]
public sealed class ServeLauncherContractTests : ControlPlaneLauncherContract, IDisposable
{
    private const string Perl = "/usr/bin/perl";
    private const UnixFileMode OwnerFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private readonly TempHome _home = new();

    private string SindriHome => Path.Combine(_home.Path, "home");

    public void Dispose() => _home.Dispose();

    protected override IControlPlaneLauncher ExitingWith(string output) => Over(Stub($"cat <<'EOF'\n{output}\nEOF\nexit 1"));

    [Fact]
    public async Task A_fresh_log_is_created_0600()
    {
        using var child = ExitingWith("hello").Launch().Value;
        await Exited(child);

        Assert.Equal(OwnerFile, File.GetUnixFileMode(child.LogPath));
    }

    [Fact]
    public async Task A_log_left_with_wider_permissions_is_narrowed_to_0600()
    {
        var log = Path.Combine(SindriHome, "logs", "serve.log");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(log)!);   // why: the log path always has a directory
        await File.WriteAllTextAsync(log, "old\n", TestContext.Current.CancellationToken);
        File.SetUnixFileMode(log, OwnerFile | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        using var child = ExitingWith("hello").Launch().Value;
        await Exited(child);

        Assert.Equal(OwnerFile, File.GetUnixFileMode(log));
    }

    [Fact]
    public async Task An_error_line_from_an_earlier_run_is_not_this_childs()
    {
        var log = Path.Combine(SindriHome, "logs", "serve.log");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(log)!);   // why: the log path always has a directory
        await File.WriteAllTextAsync(log, "SND-LEASE-001 held by host/1 since then (epoch 4)\n", TestContext.Current.CancellationToken);

        using var child = ExitingWith("hello").Launch().Value;
        await Exited(child);

        Assert.Equal("no error line in the log", child.LastError());
    }

    [Fact]
    public void The_child_runs_in_its_own_session()
    {
        Assert.SkipUnless(File.Exists(Perl), "without /usr/bin/perl the child is a plain exec in the caller's session");
        using var child = Over(Stub("exec sleep 5")).Launch().Value;
        try
        {
            WaitFor(() => Ps("comm", child.Pid).EndsWith("sleep", StringComparison.Ordinal));
            var (mine, its) = (Ps("sess", Environment.ProcessId), Ps("sess", child.Pid));
            if (mine != "0" || its != "0")
            {
                Assert.NotEqual(mine, its, StringComparer.Ordinal);
            }
            else
            {
                // macOS's ps reports no session ids; setsid also made the child the leader of a new process group
                Assert.Equal(child.Pid.ToString(CultureInfo.InvariantCulture), Ps("pgid", child.Pid));
                Assert.NotEqual(Ps("pgid", Environment.ProcessId), Ps("pgid", child.Pid), StringComparer.Ordinal);
            }
        }
        finally
        {
            using var running = Process.GetProcessById(child.Pid);
            running.Kill();
        }
    }

    [Fact]
    public async Task An_exec_that_fails_leaves_an_error_line_in_the_log()
    {
        Assert.SkipUnless(File.Exists(Perl), "without /usr/bin/perl the exec error is the shell's own text");
        var missing = Path.Combine(_home.Path, "no-such-sindri");

        using var child = Over(missing).Launch().Value;
        await Exited(child);

        Assert.StartsWith($"SND-CLI-002 exec {missing}: ", child.LastError(), StringComparison.Ordinal);
    }

    private ServeLauncher Over(string executable) => new(new SindriSettings(SindriHome, Path.Combine(_home.Path, "profile.yaml")), executable);

    private string Stub(string body)
    {
        var path = Path.Combine(_home.Path, "stub-" + Path.GetRandomFileName());
        File.WriteAllText(path, "#!/bin/sh\n" + body + "\n");
        File.SetUnixFileMode(path, OwnerFile | UnixFileMode.UserExecute);
        return path;
    }

    private static string Ps(string field, int pid)
    {
        using var ps = Process.Start(new ProcessStartInfo("/bin/ps", ["-o", field + "=", "-p", pid.ToString(CultureInfo.InvariantCulture)])
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
        })!;   // why: Process.Start returns null only for a shell-executed document, never for /bin/ps
        var text = ps.StandardOutput.ReadToEnd().Trim();
        ps.WaitForExit();
        return text;
    }

    private static void WaitFor(Func<bool> condition)
    {
        for (var waited = 0; !condition(); waited += 20)
        {
            Assert.True(waited < 5000, "the condition didn't hold within 5 s");
            Thread.Sleep(20);
        }
    }
}
