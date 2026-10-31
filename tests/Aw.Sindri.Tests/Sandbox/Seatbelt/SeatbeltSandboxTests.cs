using System.Globalization;
using System.Runtime.Versioning;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Tests.Sandbox.Bubblewrap;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Sandbox.Seatbelt;

/// <summary>The Seatbelt adapter (SB-9c) on Linux with fakes: the probe, the wrapped command, the profile file, the secrets file, the short temp.</summary>
[Lane(TestLane.Integration)]
public sealed class SeatbeltSandboxTests
{
    private const int Port = 51234;
    private const string LaunchValue = "launch-value-0123456789";
    private const string LaunchName = "AW_SINDRI_LAUNCH_SECRET";

    private static readonly SeatbeltOptions _options = new("sandbox-exec", "git", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(20));

    private static SeatbeltSandbox Sandbox(TempRunArea area, ISandboxProcesses? processes = null, ILaunchSecrets? secrets = null, int? port = Port) =>
        new(_options, processes ?? new FakeProcesses(), secrets ?? new LaunchSecretRegistry(), new FakeTimeProvider(), _ => port, _ => area.Root + "/short");

    private static LaunchSecretRegistry Secrets(TempRunArea area, string name = LaunchName, string value = LaunchValue)
    {
        var registry = new LaunchSecretRegistry();
        registry.Put(area.Plan.Launch, new Dictionary<string, string>(StringComparer.Ordinal) { [name] = value });
        return registry;
    }

    [Fact]
    public void Probe_runs_sandbox_exec_on_a_trivial_profile_and_reports_the_seatbelt_mechanism()
    {
        using var area = new TempRunArea();
        var processes = new FakeProcesses().When((p, a) => p == "sandbox-exec" && a.SequenceEqual(["-p", "(version 1)(allow default)", "/usr/bin/true"], StringComparer.Ordinal), FakeProcesses.Ok());

        Assert.Equal(SandboxMechanism.Seatbelt, Sandbox(area, processes).Probe().Value.Mechanism);
    }

    [Fact]
    public void Probe_is_014_no_seatbelt_when_sandbox_exec_fails()
    {
        using var area = new TempRunArea();
        var processes = new FakeProcesses().When((p, _) => p == "sandbox-exec", FakeProcesses.Failed("sandbox-exec: not permitted\nsecond line"));

        var result = Sandbox(area, processes).Probe();

        Assert.Equal("SND-LAUNCH-014", result.Error.Code);
        Assert.Equal(SandboxReasons.NoSeatbelt, SandboxRefusal.ReasonOf(result.Error));
    }

    [Fact]
    public void Wrap_clears_the_environment_runs_sandbox_exec_on_the_profile_and_changes_directory()
    {
        using var area = new TempRunArea();

        var argv = Sandbox(area).Wrap(area.Plan, ["claude", "--x"]).Value;

        var profile = area.Root + "/area/seatbelt.sb";
        Assert.Equal(["/usr/bin/env", "-i", "AW_SANDBOX_PLAN=" + area.Root + "/area/sandbox-plan.json", "sandbox-exec", "-f", profile, "/bin/sh", "-c"], argv.Take(8), StringComparer.Ordinal);
        Assert.Equal([area.Root + "/run", "claude", "--x"], argv.TakeLast(3), StringComparer.Ordinal);
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public async Task Wrap_writes_the_profile_0600_with_the_proxy_port()
    {
        using var area = new TempRunArea();

        _ = Sandbox(area).Wrap(area.Plan, ["claude"]).Value;

        var profile = area.Root + "/area/seatbelt.sb";
        var text = await File.ReadAllTextAsync(profile, TestContext.Current.CancellationToken);
        Assert.Contains("localhost:" + Port.ToString(CultureInfo.InvariantCulture), text, StringComparison.Ordinal);
        Assert.StartsWith("(version 1)", text, StringComparison.Ordinal);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(profile));
    }

    [Fact]
    public void Wrap_without_a_running_proxy_is_016_net_forward_and_writes_no_profile()
    {
        using var area = new TempRunArea();

        var result = Sandbox(area, port: null).Wrap(area.Plan, ["claude"]);

        Assert.Equal("SND-LAUNCH-016", result.Error.Code);
        Assert.Equal(SandboxReasons.NetForward, SandboxRefusal.ReasonOf(result.Error));
        Assert.False(File.Exists(area.Root + "/area/seatbelt.sb"));
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public async Task Launcher_values_go_through_a_0600_file_the_wrapper_sources_and_never_through_the_argv()
    {
        using var area = new TempRunArea(LaunchName);

        var argv = Sandbox(area, secrets: Secrets(area)).Wrap(area.Plan, ["claude"]).Value;

        Assert.DoesNotContain(argv, a => a.Contains(LaunchValue, StringComparison.Ordinal));
        var at = argv.ToList().IndexOf("sh");
        Assert.Equal("/bin/sh", argv[at - 2]);
        Assert.Equal(". \"$1\" && rm -f \"$1\" && shift && exec \"$@\"", argv[at - 1]);
        var file = argv[at + 1];
        Assert.StartsWith(area.Root + "/area/secrets/" + area.Plan.Launch.Value + "/", file, StringComparison.Ordinal);
        Assert.Equal("sandbox-exec", argv[at + 2]);
        Assert.Equal($"export {LaunchName}='{LaunchValue}'\n", await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
    }

    [Fact]
    public async Task A_single_quote_in_a_value_is_escaped()
    {
        using var area = new TempRunArea(LaunchName);

        var argv = Sandbox(area, secrets: Secrets(area, value: "it's")).Wrap(area.Plan, ["claude"]).Value;

        Assert.Equal($"export {LaunchName}='it'\\''s'\n", await File.ReadAllTextAsync(argv[argv.ToList().IndexOf("sh") + 1], TestContext.Current.CancellationToken));
    }

    [Fact]
    public void A_name_that_is_not_a_variable_name_is_refused()
    {
        using var area = new TempRunArea("BAD;NAME");

        var result = Sandbox(area, secrets: Secrets(area, name: "BAD;NAME")).Wrap(area.Plan, ["claude"]);

        Assert.Equal(SandboxReasons.Env, SandboxRefusal.ReasonOf(result.Error));
        Assert.False(Directory.Exists(area.Root + "/area/secrets"));
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void Wrap_empties_a_short_temp_a_crashed_launch_left_and_makes_it_0700()
    {
        using var area = new TempRunArea();
        _ = Directory.CreateDirectory(area.Root + "/short/old");
        File.WriteAllText(area.Root + "/short/old/file", "stale");

        _ = Sandbox(area).Wrap(area.Plan, ["claude"]).Value;

        Assert.Empty(Directory.GetFileSystemEntries(area.Root + "/short"));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(area.Root + "/short"));
    }

    [Fact]
    public void Discard_removes_the_short_temp_and_this_launchs_secrets()
    {
        using var area = new TempRunArea(LaunchName);
        var sandbox = Sandbox(area, secrets: Secrets(area));
        var file = sandbox.Wrap(area.Plan, ["claude"]).Value.First(a => a.Contains("/secrets/", StringComparison.Ordinal));
        Assert.True(Directory.Exists(area.Root + "/short"));

        sandbox.Discard(area.Plan);

        Assert.False(File.Exists(file));
        Assert.False(Directory.Exists(area.Root + "/short"));
    }

    [Fact]
    public async Task The_self_check_runs_every_canary_under_sandbox_exec_with_no_secret()
    {
        using var area = new TempRunArea(LaunchName);
        var processes = new FakeProcesses().When((p, _) => p == "/usr/bin/env", FakeProcesses.Ok());

        _ = await Sandbox(area, processes, Secrets(area)).SelfCheck(area.Plan, TestContext.Current.CancellationToken);

        Assert.NotEmpty(processes.Calls);
        Assert.All(processes.Calls.Where(c => c.Program != "git"), call =>
        {
            Assert.Equal("/usr/bin/env", call.Program);
            Assert.Contains("sandbox-exec", call.Arguments, StringComparer.Ordinal);
            Assert.DoesNotContain(call.Arguments, a => a.Contains(LaunchValue, StringComparison.Ordinal) || a.Contains("secrets", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void The_seatbelt_canaries_prove_a_denial_by_stat_and_read_and_need_no_gnu_tools()
    {
        using var area = new TempRunArea();
        var plan = area.Plan with { Mounts = [.. area.Plan.Mounts, new PlanMount("/denied/path", PlanMountKind.Deny, Reason: "test")] };

        var canaries = SandboxCanaries.For(plan, seatbelt: true);

        var deny = Assert.Single(canaries, c => c.Reason == SandboxReasons.FsDeny);
        Assert.Contains("check '/denied/path'", deny.Script, StringComparison.Ordinal);
        Assert.Contains("cat ", deny.Script, StringComparison.Ordinal);
        var net = Assert.Single(canaries, c => c.Reason == SandboxReasons.NetDeny);
        Assert.DoesNotContain("timeout", net.Script, StringComparison.Ordinal);
    }
}
