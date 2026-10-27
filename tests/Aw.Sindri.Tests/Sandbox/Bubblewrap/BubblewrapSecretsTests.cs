using System.Runtime.Versioning;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Testing.Sandbox;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>
/// The launch secret and a model key never appear in an argument (the F1 rule of #295): bubblewrap reads the launcher's <c lang="text">--setenv</c> pairs from a descriptor,
/// from a 0600 file that the wrapper deletes as it opens it. The canaries get none.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class BubblewrapSecretsTests
{
    private const string LaunchValue = "launch-value-0123456789";
    private const string KeyValue = "model-key-abcdefghij";

    private static readonly BubblewrapOptions _options = new("bwrap", "systemd-run", "socat", "git", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(20));

    private static LaunchSecretRegistry Registry(TempRunArea area)
    {
        var registry = new LaunchSecretRegistry();
        registry.Put(area.Plan.Launch, new Dictionary<string, string>(StringComparer.Ordinal) { ["AW_SINDRI_LAUNCH_SECRET"] = LaunchValue, ["ANTHROPIC_API_KEY"] = KeyValue, ["UNPLANNED"] = "never" });
        return registry;
    }

    private static BubblewrapSandbox Sandbox(ISandboxProcesses processes, ILaunchSecrets secrets) =>
        new(_options, processes, new HostPaths(), secrets, new FakeTimeProvider());

    [Fact]
    public void Wrap_never_puts_the_launch_secret_or_a_model_key_in_the_argv()
    {
        using var area = new TempRunArea("AW_SINDRI_LAUNCH_SECRET", "ANTHROPIC_API_KEY");

        var argv = Sandbox(new FakeProcesses(), Registry(area)).Wrap(area.Plan, ["claude", "--x"]).Value;

        Assert.DoesNotContain(argv, a => a.Contains(LaunchValue, StringComparison.Ordinal) || a.Contains(KeyValue, StringComparison.Ordinal) || a.Contains("never", StringComparison.Ordinal));
        Assert.Equal(["/bin/sh", "-c"], argv.Take(2), StringComparer.Ordinal);
        Assert.Equal("sh", argv[3]);
        Assert.StartsWith(area.Root + "/area/secrets/", argv[4], StringComparison.Ordinal);
        Assert.Equal("bwrap", argv[5]);
        Assert.Equal(["--", "claude", "--x"], argv.TakeLast(3), StringComparer.Ordinal);
        Assert.Contains("--args", argv, StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_secret_file_is_0600_and_holds_only_the_planned_names_nul_separated()
    {
        using var area = new TempRunArea("AW_SINDRI_LAUNCH_SECRET", "ANTHROPIC_API_KEY");

        var argv = Sandbox(new FakeProcesses(), Registry(area)).Wrap(area.Plan, ["claude"]).Value;

        var file = argv[4];
        var text = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);
        Assert.Equal($"--setenv\0AW_SINDRI_LAUNCH_SECRET\0{LaunchValue}\0--setenv\0ANTHROPIC_API_KEY\0{KeyValue}\0", text);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, OperatingSystem.IsWindows() ? default : File.GetUnixFileMode(file));
    }

    [Fact]
    public void A_launch_without_values_gets_the_plain_command_and_no_secrets_directory()
    {
        using var area = new TempRunArea("AW_SINDRI_LAUNCH_SECRET");

        var argv = Sandbox(new FakeProcesses(), new LaunchSecretRegistry()).Wrap(area.Plan, ["claude"]).Value;

        Assert.Equal("bwrap", argv[0]);
        Assert.False(Directory.Exists(area.Root + "/area/secrets"));
    }

    [Fact]
    public void A_wrap_that_never_ran_leaves_nothing_once_the_launcher_discards_it()
    {
        using var area = new TempRunArea("AW_SINDRI_LAUNCH_SECRET", "ANTHROPIC_API_KEY");
        var sandbox = Sandbox(new FakeProcesses(), Registry(area));
        var file = sandbox.Wrap(area.Plan, ["claude"]).Value[4];
        Assert.True(File.Exists(file));

        sandbox.Discard(area.Plan);

        Assert.False(File.Exists(file));
        Assert.False(Directory.Exists(Path.GetDirectoryName(file)));
    }

    [Fact]
    public void A_retried_wrap_and_a_stale_file_leave_exactly_the_new_file()
    {
        using var area = new TempRunArea("AW_SINDRI_LAUNCH_SECRET", "ANTHROPIC_API_KEY");
        var sandbox = Sandbox(new FakeProcesses(), Registry(area));
        var launchDirectory = area.Root + "/area/secrets/" + area.Plan.Launch.Value;
        Directory.CreateDirectory(launchDirectory);
        var stale = launchDirectory + "/stale-from-a-crash";
        File.WriteAllText(stale, "--setenv\0AW_SINDRI_LAUNCH_SECRET\0old\0");
        var first = sandbox.Wrap(area.Plan, ["claude"]).Value[4];

        var second = sandbox.Wrap(area.Plan, ["claude"]).Value[4];

        Assert.False(File.Exists(stale));
        Assert.False(File.Exists(first));
        Assert.Equal([second], Directory.GetFiles(launchDirectory), StringComparer.Ordinal);
    }

    [Fact]
    public void Two_launches_in_one_run_area_never_touch_each_others_secrets()
    {
        using var area = new TempRunArea("AW_SINDRI_LAUNCH_SECRET", "ANTHROPIC_API_KEY");
        var registry = Registry(area);
        var other = area.Plan with { Launch = LaunchId.From("LCH-" + new string('e', 32)) };
        registry.Put(other.Launch, new Dictionary<string, string>(StringComparer.Ordinal) { ["AW_SINDRI_LAUNCH_SECRET"] = "other-launch-value" });
        var sandbox = Sandbox(new FakeProcesses(), registry);
        var first = sandbox.Wrap(area.Plan, ["claude"]).Value[4];

        var second = sandbox.Wrap(other, ["claude"]).Value[4];   // another launch's Wrap does not sweep the first's file
        Assert.True(File.Exists(first));
        sandbox.Discard(other);   // nor does its Discard
        Assert.True(File.Exists(first));
        Assert.False(File.Exists(second));
        var otherAgain = sandbox.Wrap(other, ["claude"]).Value[4];
        var firstAgain = sandbox.Wrap(area.Plan, ["claude"]).Value[4];   // re-wrapping the first replaces only the first's file

        Assert.False(string.Equals(first, firstAgain, StringComparison.Ordinal));
        Assert.False(File.Exists(first));
        Assert.True(File.Exists(firstAgain));
        Assert.True(File.Exists(otherAgain));
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void A_discard_that_cannot_delete_warns_and_does_not_throw()
    {
        using var area = new TempRunArea("AW_SINDRI_LAUNCH_SECRET", "ANTHROPIC_API_KEY");
        List<string> warnings = [];
        var sandbox = new BubblewrapSandbox(_options, new FakeProcesses(), new HostPaths(), Registry(area), new FakeTimeProvider(), warnings.Add);
        var directory = Path.GetDirectoryName(sandbox.Wrap(area.Plan, ["claude"]).Value[4])!;
        File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserExecute);   // a file cannot be removed from a read-only directory
        try
        {
            var thrown = Record.Exception(() => sandbox.Discard(area.Plan));

            Assert.Null(thrown);
            Assert.Contains(directory, Assert.Single(warnings), StringComparison.Ordinal);
        }
        finally
        {
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact]
    public void A_discard_of_a_plan_that_was_never_wrapped_does_nothing()
    {
        using var area = new TempRunArea("AW_SINDRI_LAUNCH_SECRET");

        var thrown = Record.Exception(() => Sandbox(new FakeProcesses(), Registry(area)).Discard(area.Plan));

        Assert.Null(thrown);
    }

    [Fact]
    public async Task The_canaries_are_wrapped_without_secrets()
    {
        using var area = new TempRunArea("AW_SINDRI_LAUNCH_SECRET", "ANTHROPIC_API_KEY");
        var processes = new FakeProcesses().When((p, _) => p == "bwrap", FakeProcesses.Ok());

        _ = await Sandbox(processes, Registry(area)).SelfCheck(area.Plan, TestContext.Current.CancellationToken);

        Assert.NotEmpty(processes.Calls);
        Assert.All(processes.Calls.Where(c => c.Program != "git"), call =>
        {
            Assert.Equal("bwrap", call.Program);
            Assert.DoesNotContain(call.Arguments, a => a.Contains(LaunchValue, StringComparison.Ordinal) || a.Contains(KeyValue, StringComparison.Ordinal));
            Assert.DoesNotContain("--args", call.Arguments, StringComparer.Ordinal);
        });
        Assert.False(Directory.Exists(area.Root + "/area/secrets"));
    }

    [Fact]
    public void The_secret_file_is_gone_after_the_wrapper_ran_and_the_agent_sees_the_value_but_no_open_descriptor_to_it()
    {
        Assert.SkipUnless(BubblewrapBox.Unavailable is null, BubblewrapBox.Unavailable ?? string.Empty);
        using var area = new TempRunArea("AW_SINDRI_LAUNCH_SECRET");
        var registry = new LaunchSecretRegistry();
        registry.Put(area.Plan.Launch, new Dictionary<string, string>(StringComparer.Ordinal) { ["AW_SINDRI_LAUNCH_SECRET"] = LaunchValue });
        var sandbox = new BubblewrapSandbox(BubblewrapBox.Options, new SandboxProcesses(), new HostPaths(), registry, RealTime.Clock);

        var argv = sandbox.Wrap(area.Plan, ["/bin/sh", "-c", "printenv AW_SINDRI_LAUNCH_SECRET; ls -l /proc/self/fd 2>&1"]).Value;
        var run = SandboxBox.Run(argv[0], [.. argv.Skip(1)]);

        Assert.True(run.Exit == 0, run.Output);
        Assert.StartsWith(LaunchValue, run.Output, StringComparison.Ordinal);
        Assert.False(File.Exists(argv[4]));
        Assert.DoesNotContain("secrets", run.Output, StringComparison.Ordinal);   // no descriptor onto the (deleted) file survives into the agent
        Assert.DoesNotContain("(deleted)", run.Output, StringComparison.Ordinal);
    }
}
