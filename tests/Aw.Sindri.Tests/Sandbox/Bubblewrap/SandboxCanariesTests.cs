using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Tests.Sandbox.Plan;
using Microsoft.Extensions.Time.Testing;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>The self-check (sandbox spec §6) as scripts built from the plan, and how a failing one becomes SND-LAUNCH-016 with the canary's name.</summary>
[Lane(TestLane.Unit)]
public sealed class SandboxCanariesTests
{
    private static readonly BubblewrapOptions _options = new("bwrap", "systemd-run", "socat", "git", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(20));
    private static readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero));

    private static BubblewrapSandbox Sandbox(FakeProcesses processes, ILaunchSecrets? secrets = null) => new(_options, processes, new HostPaths(), secrets ?? new LaunchSecretRegistry(), _clock);

    private static string[] Reasons(SandboxPlan plan) => [.. SandboxCanaries.For(plan).Select(c => c.Reason)];

    [Fact]
    public void A_write_scope_plan_has_every_canary_this_adapter_owns_in_order() =>
        Assert.Equal(["fs-deny", "fs-write", "git-ref", "env", "login", "hooks-run", "net-deny"], Reasons(SandboxPlannerTests.Build(Req.WriteScope)));

    [Fact]
    public void A_read_only_plan_has_no_git_ref_canary_and_proves_the_worktree_is_not_writable()
    {
        var plan = SandboxPlannerTests.Build(Req.ReadOnly);

        Assert.DoesNotContain("git-ref", Reasons(plan), StringComparer.Ordinal);
        Assert.Contains("&& fail", SandboxCanaries.For(plan).Single(c => c.Reason == "fs-write").Script, StringComparison.Ordinal);
    }

    [Fact]
    public void Git_canary_never_deletes_a_ref_and_checks_the_effective_settings_on_the_real_worktree()
    {
        var script = SandboxCanaries.For(SandboxPlannerTests.Build(Req.WriteScope)).Single(c => c.Reason == "git-ref").Script;

        Assert.DoesNotContain("-d ", script, StringComparison.Ordinal);
        Assert.DoesNotContain("branch", script, StringComparison.Ordinal);
        Assert.Contains($"cd '{Req.Worktree}'", script, StringComparison.Ordinal);
        Assert.Contains("git config --get core.hooksPath", script, StringComparison.Ordinal);
        Assert.Contains("refs/heads/sindri/x/1/selfcheck", script, StringComparison.Ordinal);
        Assert.Contains("refs/heads/other-selfcheck HEAD >/dev/null 2>&1 && fail", script, StringComparison.Ordinal);
    }

    [Fact]
    public void The_hook_canary_has_the_three_parts_and_runs_under_bash()
    {
        var canary = SandboxCanaries.For(SandboxPlannerTests.Build(Req.WriteScope)).Single(c => c.Reason == "hooks-run");

        Assert.Equal("/bin/bash", canary.Shell);
        Assert.Contains("block-destructive.sh", canary.Script, StringComparison.Ordinal);
        Assert.Contains("detect-secrets.sh", canary.Script, StringComparison.Ordinal);
        Assert.Contains("judge brief get task:selfcheck", canary.Script, StringComparison.Ordinal);
        Assert.Contains("judge/sessions", canary.Script, StringComparison.Ordinal);
        Assert.Contains("aw_failopen", canary.Script, StringComparison.Ordinal);
        Assert.Contains("meta-outbox.sh", canary.Script, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("block-destructive", "block-destructive.sh")]
    [InlineData("detect-secrets", "detect-secrets.sh")]
    [InlineData("meta-outbox", "meta-outbox.sh")]
    public void The_hook_canary_runs_a_hook_only_while_the_plan_keeps_it(string hook, string script)
    {
        var plan = SandboxPlannerTests.Build(Req.WriteScope);
        var without = plan with { Hooks = plan.Hooks with { Kept = [.. plan.Hooks.Kept.Where(h => !string.Equals(h, hook, StringComparison.Ordinal))] } };

        Assert.Contains(script, SandboxCanaries.For(plan).Single(c => c.Reason == "hooks-run").Script, StringComparison.Ordinal);
        Assert.DoesNotContain(script, SandboxCanaries.For(without).Single(c => c.Reason == "hooks-run").Script, StringComparison.Ordinal);
    }

    [Fact]
    public void The_hook_canary_never_exercises_a_dropped_or_rewriting_hook()
    {
        var script = SandboxCanaries.For(SandboxPlannerTests.Build(Req.WriteScope)).Single(c => c.Reason == "hooks-run").Script;

        foreach (var hook in new[] { "external-write-guard", "record-teammate-name", "scope-gate", "send-gate", "rtk-rewrite" })
        {
            Assert.DoesNotContain(hook, script, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_login_canary_checks_the_copy_the_plan_names_and_the_originals_mask()
    {
        var script = SandboxCanaries.For(SandboxPlannerTests.Build(Req.WriteScope)).Single(c => c.Reason == "login").Script;

        Assert.Contains($"'{Req.Home}/.claude-run/.credentials.json'", script, StringComparison.Ordinal);
        Assert.Contains("= 600", script, StringComparison.Ordinal);
        Assert.Contains("$HOME/.claude/.credentials.json", script, StringComparison.Ordinal);
    }

    [Fact]
    public void The_env_canary_flags_a_name_off_the_allowlist_and_the_held_slot_marker()
    {
        var canary = SandboxCanaries.For(SandboxPlannerTests.Build(Req.WriteScope)).Single(c => c.Reason == "env");

        Assert.Null(canary.Judge!(FakeProcesses.Ok("PATH=/bin\nHOME=/h\nAW_SANDBOX=1\n")));
        Assert.Contains("GH_TOKEN", canary.Judge!(FakeProcesses.Ok("PATH=/bin\nGH_TOKEN=x\n")), StringComparison.Ordinal);
        Assert.Contains("AW_HEAVY_LOCK_HELD", canary.Judge!(FakeProcesses.Ok("PATH=/bin\nAW_HEAVY_LOCK_HELD=1\n")), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("it's", "'it'\\''s'")]
    [InlineData("a b", "'a b'")]
    public void Quote_closes_escapes_and_reopens_single_quotes(string value, string expected) => Assert.Equal(expected, SandboxCanaries.Quote(value));

    [Fact]
    public async Task A_failing_canary_is_016_naming_the_canary_and_what_was_observed()
    {
        using var area = new TempRunArea();
        var plan = area.Plan;
        var processes = new FakeProcesses().When((p, _) => p == "bwrap", FakeProcesses.Failed("visible: /home/owner/.ssh"));
        var sandbox = Sandbox(processes);

        var result = await sandbox.SelfCheck(plan, TestContext.Current.CancellationToken);

        Assert.Equal("SND-LAUNCH-016", result.Error.Code);
        Assert.Equal("fs-deny", SandboxRefusal.ReasonOf(result.Error));
        Assert.Contains("visible: /home/owner/.ssh", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_worktree_is_015_through_the_self_check_before_any_canary_runs_and_is_never_created()
    {
        using var area = new TempRunArea();
        var plan = area.Plan with { Worktree = area.Root + "/no-such-worktree" };
        var processes = new FakeProcesses().When((p, _) => p == "bwrap", FakeProcesses.Ok());

        var result = await Sandbox(processes).SelfCheck(plan, TestContext.Current.CancellationToken);

        Assert.Equal("SND-LAUNCH-015", result.Error.Code);
        Assert.DoesNotContain(processes.Calls, c => c.Program == "bwrap");
        Assert.False(Directory.Exists(area.Root + "/no-such-worktree"));
    }

    [Fact]
    public async Task A_missing_run_directory_is_016_fs_write_through_the_self_check_not_a_deny_canary()
    {
        using var area = new TempRunArea();
        var plan = area.Plan with { RunDirectory = area.Root + "/no-such-run" };
        var processes = new FakeProcesses().When((p, _) => p == "bwrap", FakeProcesses.Ok());

        var result = await Sandbox(processes).SelfCheck(plan, TestContext.Current.CancellationToken);

        Assert.Equal("SND-LAUNCH-016", result.Error.Code);
        Assert.Equal("fs-write", SandboxRefusal.ReasonOf(result.Error));
        Assert.DoesNotContain(processes.Calls, c => c.Program == "bwrap");
    }

    [Fact]
    public async Task A_run_area_that_cannot_be_made_is_016_fs_write_before_any_canary_runs()
    {
        using var area = new TempRunArea();
        await File.WriteAllTextAsync(area.Root + "/a-file", "x", TestContext.Current.CancellationToken);
        var plan = area.Plan with { Mounts = [.. area.Plan.Mounts, new PlanMount(area.Root + "/a-file/below", PlanMountKind.ReadWrite, Reason: "test")] };   // a directory below a file cannot be made
        var processes = new FakeProcesses().When((p, _) => p == "bwrap", FakeProcesses.Ok());

        var result = await Sandbox(processes).SelfCheck(plan, TestContext.Current.CancellationToken);

        Assert.Equal("SND-LAUNCH-016", result.Error.Code);
        Assert.Equal("fs-write", SandboxRefusal.ReasonOf(result.Error));
        Assert.Contains("could not be prepared", result.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(processes.Calls, c => c.Program == "bwrap");
    }

    [Fact]
    public async Task The_whole_self_check_has_one_deadline_not_one_per_canary()
    {
        using var area = new TempRunArea();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero));
        var options = new BubblewrapOptions("bwrap", "systemd-run", "socat", "git", TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(4));
        var processes = new FakeProcesses { OnRun = () => clock.Advance(TimeSpan.FromSeconds(2)) }.When((p, _) => p == "bwrap", FakeProcesses.Ok());

        var result = await new BubblewrapSandbox(options, processes, new HostPaths(), new LaunchSecretRegistry(), clock).SelfCheck(area.Plan, TestContext.Current.CancellationToken);

        Assert.Equal("SND-LAUNCH-016", result.Error.Code);
        Assert.Contains("took longer than 4 s", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(2, processes.Calls.Count(c => c.Program == "bwrap"));
    }

    [Fact]
    public async Task The_selfcheck_ref_is_removed_from_outside_after_a_write_scope_self_check_even_when_a_canary_fails()
    {
        using var area = new TempRunArea();
        var plan = area.Plan with { Worktree = area.Root + "/run", Mounts = [.. area.Plan.Mounts, new PlanMount(area.Root + "/run", PlanMountKind.ReadWrite, Optional: true)] };
        var processes = new FakeProcesses().When((p, _) => p == "bwrap", FakeProcesses.Failed("boom"));
        _ = processes.When((p, _) => p == "git", FakeProcesses.Ok());
        var sandbox = Sandbox(processes);

        _ = await sandbox.SelfCheck(plan, TestContext.Current.CancellationToken);

        var refs = processes.Calls.Where(c => c.Program == "git").Select(c => c.Arguments[^1]).ToList();
        Assert.Equal(["refs/heads/sindri/x/1/selfcheck", "refs/heads/other-selfcheck"], refs);
        Assert.All(processes.Calls.Where(c => c.Program == "git"), c => Assert.Equal(["-C", area.Root + "/run", "-c", "core.hooksPath=/dev/null", "-c", "core.fsmonitor=", "update-ref", "-d"], c.Arguments.Take(8), StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_login_whose_copy_directory_the_inner_policy_does_not_deny_fails_the_login_canary_before_anything_runs()
    {
        var plan = SandboxPlannerTests.Build(Req.WriteScope);
        var open = plan with { Policy = plan.Policy with { Rules = [.. plan.Policy.Rules.Where(r => !(r.Effect == InnerRuleEffect.Deny && string.Equals(r.Tool, "Read", StringComparison.Ordinal) && r.Pattern.StartsWith(Req.Home + "/.claude-run", StringComparison.Ordinal)))] } };
        var processes = new FakeProcesses();

        var result = await Sandbox(processes).SelfCheck(open, TestContext.Current.CancellationToken);

        Assert.Equal("login", SandboxRefusal.ReasonOf(result.Error));
        Assert.Empty(processes.Calls);
    }

    [Fact]
    public void Wrap_without_launcher_values_is_plain_bwrap_first_and_the_argv_after_dash_dash()
    {
        using var area = new TempRunArea();
        var argv = Sandbox(new FakeProcesses()).Wrap(area.Plan, ["claude", "--x"]).Value;

        Assert.Equal("bwrap", argv[0]);
        Assert.Equal(["--", "claude", "--x"], argv.TakeLast(3), StringComparer.Ordinal);
    }
}
