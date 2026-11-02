using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Features.CheckSandbox;
using Aw.Sindri.Testing.Sandbox;
using Aw.Sindri.Tests.Sandbox;

namespace Aw.Sindri.Tests.CheckSandbox;

/// <summary>Doctor's sandbox rows say what the Check again card says (sandbox spec §8): one sentence per (code, reason), the command as the fix.</summary>
[Lane(TestLane.Unit)]
public sealed class SandboxDoctorChecksTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SandboxBlocker Blocker(ErrorDefinitionPair pair, ModelLoginSource? source = null) =>
        SandboxBlocker.Of(SandboxRefusal.For(pair.Error, pair.Reason, source: source));

    private const long Gib = 1024L * 1024 * 1024;

    private static Task<IReadOnlyList<DoctorCheck>> Run(ISandboxChecks checks, ISandbox? sandbox = null, SandboxSettings? settings = null, long ramGib = 62) =>
        new SandboxDoctorChecks(checks, sandbox ?? new PassThroughSandbox(), TimeProvider.System, new Approved(settings), new Machine(ramGib * Gib)).Run(Token).AsTask();

    /// <summary>The rows the checks' blockers or ready line make: not the limits, hooks and inner rows.</summary>
    private static bool IsProbeRow(DoctorCheck row) => string.Equals(row.Name, "sandbox", StringComparison.Ordinal) || string.Equals(row.Name, "sandbox:login", StringComparison.Ordinal);

    private static string Golden(string id) =>
        (string)SandboxGoldens.Object("doctor.json")["lines"]!.AsArray().Single(l => (string)l!["id"]! == id)!["text"]!;

    private static DoctorCheck Row(IReadOnlyList<DoctorCheck> rows, string name) => rows.Single(r => string.Equals(r.Name, name, StringComparison.Ordinal));

    [Fact]
    public async Task A_ready_box_says_so_with_the_mechanism_and_the_checks_passed()
    {
        var rows = await Run(new Report());

        var row = Assert.Single(rows, IsProbeRow);
        Assert.Equal(("sandbox", CheckState.Ok), (row.Name, row.State));
        Assert.StartsWith("sandbox ready: test-only pass-through, 4 checks passed in ", row.Detail, StringComparison.Ordinal);
        Assert.EndsWith(" ms", row.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_blocker_is_its_golden_sentence_with_the_command_as_the_fix()
    {
        var rows = await Run(new Report(Blocker(new(SindriErrors.LaunchSandboxUnavailable, SandboxReasons.NoUserManager))));

        var row = Assert.Single(rows, IsProbeRow);
        Assert.Equal(("sandbox", CheckState.Fail), (row.Name, row.State));
        Assert.Equal("The sandbox needs your user session manager, which isn't running. Run `loginctl enable-linger $USER`, then press Check again.", row.Detail);
        Assert.Equal("loginctl enable-linger $USER", row.Fix);
    }

    [Fact]
    public async Task A_login_blocker_is_the_login_row_and_picks_its_sentence_by_source()
    {
        var rows = await Run(new Report(Blocker(new(SindriErrors.LaunchLoginUnavailable, SandboxReasons.NoLogin), ModelLoginSource.OwnerCopy)));

        var row = Assert.Single(rows, IsProbeRow);
        Assert.Equal(("sandbox:login", CheckState.Fail), (row.Name, row.State));
        Assert.Equal("Claude Code (or Codex) isn't signed in for Steps. Sign in as yourself, then press Check again.", row.Detail);
    }

    [Fact]
    public async Task Unsupported_platform_is_gray_information_never_a_fault()
    {
        var rows = await Run(new Report(Blocker(new(SindriErrors.LaunchSandboxUnavailable, SandboxReasons.UnsupportedPlatform))));

        var row = Assert.Single(rows, IsProbeRow);
        Assert.Equal(CheckState.Ok, row.State);
        Assert.Null(row.Fix);
        Assert.Equal("Steps can't run by themselves on macOS yet.", row.Detail);
    }

    [Fact]
    public async Task Every_blocker_gets_its_own_row()
    {
        var rows = await Run(new Report(
            Blocker(new(SindriErrors.LaunchSandboxUnavailable, SandboxReasons.NoForwarder)),
            Blocker(new(SindriErrors.LaunchUnconfinable, SandboxReasons.FormNone))));

        Assert.Equal(2, rows.Count(IsProbeRow));
    }

    [Fact]
    public async Task Limits_say_sessions_times_memory_against_the_machine()
    {
        var rows = await Run(new Report(), settings: new SandboxSettings(Limits: new SandboxLimits(Memory: "12GiB", Sessions: 3)));

        var row = Row(rows, "sandbox:limits");
        Assert.Equal(CheckState.Ok, row.State);
        Assert.Equal(Golden("limits").Replace("limits: ", string.Empty, StringComparison.Ordinal), row.Detail);
    }

    [Fact]
    public async Task Limits_warn_when_the_sessions_would_not_fit_in_the_machine()
    {
        var rows = await Run(new Report(), settings: new SandboxSettings(Limits: new SandboxLimits(Memory: "12GiB", Sessions: 3)), ramGib: 16);

        var row = Row(rows, "sandbox:limits");
        Assert.Equal(CheckState.Warn, row.State);
        Assert.Equal(Golden("limits-over").Replace("limits: ", string.Empty, StringComparison.Ordinal), row.Detail);
    }

    [Fact]
    public async Task Hooks_say_not_checked_yet_because_nothing_records_the_canary_result()
    {
        var row = Row(await Run(new Report()), "sandbox:hooks");

        Assert.Equal(CheckState.Ok, row.State);
        Assert.Equal(Golden("hooks-unchecked").Replace("hooks: ", string.Empty, StringComparison.Ordinal), row.Detail);
    }

    [Fact]
    public async Task The_agents_own_sandboxes_are_off_unless_the_profile_turns_them_on()
    {
        var off = Row(await Run(new Report()), "sandbox:inner");
        var on = Row(await Run(new Report(), settings: new SandboxSettings(Inner: InnerSandbox.On)), "sandbox:inner");

        Assert.Equal(CheckState.Ok, off.State);
        Assert.Equal(Golden("inner").Replace("agents' own sandboxes: ", string.Empty, StringComparison.Ordinal), off.Detail);
        Assert.Equal("on", on.Detail);
    }

    [Fact]
    public async Task A_ready_box_says_sessions_the_human_opens_are_sandboxed_too()
    {
        var row = Row(await Run(new Report()), "sandbox:attended");

        Assert.Equal(CheckState.Ok, row.State);
        Assert.Null(row.Fix);
        Assert.Equal(Golden("attended").Replace("sessions you open: ", string.Empty, StringComparison.Ordinal), row.Detail);
    }

    [Fact]
    public async Task A_blocked_box_has_no_attended_row_because_nothing_is_sandboxed_there()
    {
        var rows = await Run(new Report(Blocker(new(SindriErrors.LaunchSandboxUnavailable, SandboxReasons.NoUserManager))));

        Assert.DoesNotContain(rows, r => string.Equals(r.Name, "sandbox:attended", StringComparison.Ordinal));
    }

    private sealed class Machine(long ram) : ISandboxMachine
    {
        public long RamBytes => ram;

        public int Cores => 8;
    }

    private sealed class Approved(SandboxSettings? settings) : IApprovedProfile
    {
        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken)
        {
            if (settings is null)
            {
                return ValueTask.FromResult(Results.Success<LoadedProfile?>(value: null));
            }

            var profile = new SindriProfile(1, "thor", Sandbox: settings);
            return ValueTask.FromResult(Results.Success<LoadedProfile?>(new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml")));
        }
    }

    internal readonly record struct ErrorDefinitionPair(ErrorDefinition Error, string Reason);

    private sealed class Report(params SandboxBlocker[] blockers) : ISandboxChecks
    {
        public ValueTask<SandboxCheckReport> Run(CancellationToken cancellationToken) => ValueTask.FromResult(new SandboxCheckReport(blockers, []));
    }
}
