using Aw.BugFix.Core.Ports;
using Aw.BugFix.Infrastructure;
using Aw.BugFix.Testing;
using Aw.Kernel.Testing;

namespace Aw.BugFix.Tests.Contracts;

[Lane(TestLane.Integration)]
public sealed class ProcessCheckRunnerContractTests : CheckRunnerContract, IDisposable
{
    private readonly TempDirectory _temp = new();

    protected override ICheckRunner Runner { get; } = new ProcessCheckRunner(TestSettings.For(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(1)), new LocalFiles());

    protected override string Directory => _temp.Path;

    protected override ICheckRunner HangingRunner { get; } = new ProcessCheckRunner(TestSettings.For(TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(1)), new LocalFiles());

    protected override IReadOnlyList<string> HangingCommand => ["sleep", "5"];

    protected override IReadOnlyList<string> PassingCommand => ["sh", "-c", "echo ok"];

    protected override IReadOnlyList<string> FailingCommand => ["sh", "-c", "echo not ok; exit 1"];

    protected override IReadOnlyList<string> KilledCommand => ["sh", "-c", "echo started; kill -KILL $$"];

    protected override IReadOnlyList<string> SilentCommand => ["sh", "-c", "exit 1"];

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void The_log_holds_the_output_and_a_hang_is_killed_and_fails()
    {
        var log = Path.Combine(_temp.Path, "echo.log");
        Assert.Equal((3, true), Runner.Run(["sh", "-c", "echo out; echo err >&2; exit 3"], Directory, log, Key) is var e ? (e.Code, e.Ran) : default);
        Assert.Equal(["err", "out"], File.ReadAllLines(log).Order(StringComparer.Ordinal), StringComparer.Ordinal);   // two streams, one log: their order isn't fixed

        var quick = new ProcessCheckRunner(TestSettings.For(TimeSpan.FromMilliseconds(300), TimeSpan.FromSeconds(1)), new LocalFiles());
        Assert.False(quick.Run(["sleep", "5"], Directory, Path.Combine(_temp.Path, "sleep.log"), Key).Ran);
    }

    [Fact]
    public void A_background_child_holding_the_output_keeps_the_exit_code_and_does_not_stretch_the_run()
    {
        var runner = new ProcessCheckRunner(TestSettings.For(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5)), new LocalFiles());
        var clock = System.Diagnostics.Stopwatch.StartNew();

        var exit = runner.Run(["sh", "-c", "sleep 30 & echo started; exit 1"], Directory, Path.Combine(_temp.Path, "bg.log"), Key);

        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(15), $"took {clock.Elapsed}");
        Assert.Equal((1, true), (exit.Code, exit.Ran));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Contains("left 1 processes running; they were stopped", File.ReadAllText(Path.Combine(_temp.Path, "bg.log")), StringComparison.Ordinal);   // its child is stopped, not left to run on
        }
    }

    [Fact]
    public void At_the_limit_the_check_gets_to_run_its_exit_trap()
    {
        SkipUnlessLinux();
        var marker = Path.Combine(_temp.Path, "trapped");
        var runner = new ProcessCheckRunner(TestSettings.For(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5)), new LocalFiles());
        var clock = System.Diagnostics.Stopwatch.StartNew();

        // the shell sits in a foreground sleep: it only acts on TERM once that ends, so TERM has to reach the sleep too.
        var exit = runner.Run(["sh", "-c", "trap 'exit 143' TERM; trap 'echo trapped >\"$1\"' EXIT; sleep 60", "sh", marker], Directory, Path.Combine(_temp.Path, "trap.log"), Key);

        Assert.False(exit.Ran);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(30), $"took {clock.Elapsed}");
        Assert.True(File.Exists(marker), "the EXIT trap did not run");
        Assert.DoesNotContain("ignored the stop request", File.ReadAllText(Path.Combine(_temp.Path, "trap.log")), StringComparison.Ordinal);
    }

    [Fact]
    public void A_daemonized_child_is_gone_after_the_limit()
    {
        SkipUnlessLinux();
        var token = "gh1130-" + RealTime.NewGuid().ToString("N");
        var runner = new ProcessCheckRunner(TestSettings.For(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5)), new LocalFiles());
        try
        {
            var exit = runner.Run(["bash", "-c", Daemonize + "sleep 60", "bash", token], Directory, Path.Combine(_temp.Path, "limit.log"), Key);

            Assert.False(exit.Ran);
            Assert.Empty(WithToken(token));
        }
        finally
        {
            KillOnly(token);
        }
    }

    [Fact]
    public void A_daemonized_child_is_gone_after_a_normal_exit()
    {
        SkipUnlessLinux();
        var token = "gh1130-" + RealTime.NewGuid().ToString("N");
        var runner = new ProcessCheckRunner(TestSettings.For(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5)), new LocalFiles());
        try
        {
            var exit = runner.Run(["bash", "-c", Daemonize + "exit 3", "bash", token], Directory, Path.Combine(_temp.Path, "exit.log"), Key);

            Assert.Equal((3, true), (exit.Code, exit.Ran));
            Assert.Empty(WithToken(token));
            Assert.Contains("left 1 processes running; they were stopped", File.ReadAllText(Path.Combine(_temp.Path, "exit.log")), StringComparison.Ordinal);
        }
        finally
        {
            KillOnly(token);
        }
    }

    // A double fork into its own session (the way a fixture's serve runs), named after the token in $1 so the test can find it.
    private const string Daemonize = "( setsid bash -c 'exec -a \"$0\" sleep 300' \"$1\" </dev/null >/dev/null 2>&1 & ); sleep 0.5; ";

    private static void SkipUnlessLinux() => Assert.SkipUnless(OperatingSystem.IsLinux(), "reads /proc and needs setsid");

    // Every PID whose command line carries the token: the daemonized sleep (exec -a) and the bash that was given it.
    private static List<int> WithToken(string token)
    {
        var found = new List<int>();
        foreach (var directory in System.IO.Directory.EnumerateDirectories("/proc"))
        {
            try
            {
                if (int.TryParse(Path.GetFileName(directory), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var pid) && File.ReadAllText(Path.Combine(directory, "cmdline")).Contains(token, StringComparison.Ordinal))
                {
                    found.Add(pid);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // exited meanwhile, or not ours.
            }
        }

        return found;
    }

    private static void KillOnly(string token)
    {
        foreach (var pid in WithToken(token))
        {
            try
            {
                using var process = System.Diagnostics.Process.GetProcessById(pid);
                process.Kill();
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException)
            {
                // already gone.
            }
        }
    }

    [Fact]
    public void Only_the_allowed_variables_reach_the_check()
    {
        var log = Path.Combine(_temp.Path, "env.log");
        string[] shellOwn = ["PATH", "HOME", "PWD", "OLDPWD", "SHLVL", "_"];
        var inherited = Environment.GetEnvironmentVariables().Keys.Cast<string>().Order(StringComparer.Ordinal)
            .FirstOrDefault(n => !shellOwn.Contains(n, StringComparer.Ordinal) && !CheckEnvironment.Frozen.Contains(n, StringComparer.Ordinal) && !CheckEnvironment.PassedThrough.Contains(n, StringComparer.Ordinal));
        if (inherited is null)
        {
            Assert.Skip("this process has no variable outside the allowlist to probe with");
        }

        // A variable this process has and the settings don't allow: the check must not see it.
        var exit = Runner.Run(["sh", "-c", "printenv \"$1\" || echo unset; echo \"${HOME:+home}\"", "sh", inherited], Directory, log, Key);

        Assert.True(exit.Ran);
        Assert.Equal(["unset", "home"], File.ReadAllLines(log), StringComparer.Ordinal);
    }

    [Fact]
    public void The_frozen_variables_are_kept_as_digests_never_as_values()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "/usr/bin:/bin";
        var runner = new ProcessCheckRunner(
            BugFixSettings.Read(name => name switch { "PATH" => path, "TZ" => "UTC", "DATABASE_URL" => "postgres://u:" + "secret@db/x", "AW_BUGFIX_CHECK_ENV" => "TZ,DATABASE_URL", _ => null }),
            new LocalFiles());

        var exit = runner.Run(["sh", "-c", "echo \"$TZ $DATABASE_URL\""], Directory, Path.Combine(_temp.Path, "digest.log"), Key);   // TZ is passed through and frozen: no duplicate

        Assert.Equal("UTC postgres://u:" + "secret@db/x", File.ReadAllText(Path.Combine(_temp.Path, "digest.log")).Trim());
        Assert.Equal(["DATABASE_URL", "PATH", "TZ"], exit.Environment.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.All(exit.Environment.Values, v => Assert.Matches("^hmac-sha256:[0-9a-f]{64}$", v));
        Assert.DoesNotContain(exit.Environment.Values, v => v.Contains("secret", StringComparison.Ordinal));
    }

    [Fact]
    public void DISPLAY_reaches_the_check_only_when_AW_BUGFIX_CHECK_ENV_names_it()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "/usr/bin:/bin";
        var plain = new ProcessCheckRunner(BugFixSettings.Read(name => name switch { "PATH" => path, "DISPLAY" => ":0", _ => null }), new LocalFiles());
        var named = new ProcessCheckRunner(BugFixSettings.Read(name => name switch { "PATH" => path, "DISPLAY" => ":0", "AW_BUGFIX_CHECK_ENV" => "DISPLAY", _ => null }), new LocalFiles());
        string[] probe = ["sh", "-c", "echo \"[${DISPLAY-unset}]\""];

        _ = plain.Run(probe, Directory, Path.Combine(_temp.Path, "plain.log"), Key);
        _ = named.Run(probe, Directory, Path.Combine(_temp.Path, "named.log"), Key);

        Assert.Equal("[unset]", File.ReadAllText(Path.Combine(_temp.Path, "plain.log")).Trim());
        Assert.Equal("[:0]", File.ReadAllText(Path.Combine(_temp.Path, "named.log")).Trim());
    }

    [Fact]
    public void A_relative_program_starts_from_the_runs_directory()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var script = Path.Combine(_temp.Path, "check.sh");
        _ = ExecutableScript.Write(script, "echo ran; exit 4\n");

        var exit = Runner.Run(["./check.sh"], Directory, Path.Combine(_temp.Path, "rel.log"), Key);

        Assert.Equal((4, true), (exit.Code, exit.Ran));
        Assert.Equal(new LocalFiles().Sha256(script).Value, exit.ProgramSha256);
    }

    [Fact]
    public void A_symlinked_program_runs_under_the_name_it_was_found_by()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var target = Path.Combine(_temp.Path, "dispatch.sh");
        _ = ExecutableScript.Write(target, "echo ran; [ \"$(basename \"$0\")\" = alias ] && exit 0 || exit 5\n");
        _ = File.CreateSymbolicLink(Path.Combine(_temp.Path, "alias"), target);

        var exit = Runner.Run(["./alias"], Directory, Path.Combine(_temp.Path, "alias.log"), Key);

        Assert.Equal((0, true), (exit.Code, exit.Ran));   // a venv's python or bunx keeps its name
        Assert.Equal(new LocalFiles().Sha256(target).Value, exit.ProgramSha256);
    }
}
