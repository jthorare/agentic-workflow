using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>The Seatbelt form of the plan (sandbox spec §3.1, SB-9a): real paths only, HOME as a folder of links, denies before the entries they cover.</summary>
public sealed class SandboxPlanSeatbeltTests
{
    private const string RunTmp = Req.RunArea + "/tmp";
    private const string RunHome = Req.RunArea + "/home";

    private static SandboxPlan Build(SandboxRequest request, SandboxMechanism mechanism = SandboxMechanism.Seatbelt)
    {
        var result = SandboxPlanner.Build(mechanism, request);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    private static string? Env(SandboxPlan plan, string name) => plan.Environment.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal))?.Value;

    private static int IndexOf(SandboxPlan plan, string path, PlanMountKind kind) =>
        plan.Mounts.ToList().FindLastIndex(m => string.Equals(m.Path, path, StringComparison.Ordinal) && m.Kind == kind);

    [Fact, Trait("Lane", "unit")]
    public void A_seatbelt_plan_has_no_remapped_entry_except_links_to_real_paths()
    {
        var plan = Build(Req.WriteScope);

        Assert.All(plan.Mounts.Where(m => m.Kind != PlanMountKind.Link), m => Assert.Null(m.Source));
        var links = plan.Mounts.Where(m => m.Kind == PlanMountKind.Link).ToList();
        Assert.NotEmpty(links);
        Assert.All(links, l => Assert.StartsWith("/", l.Source, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_seatbelt_plan_has_no_device_proc_run_or_net_entries()
    {
        var plan = Build(Req.WriteScope);

        Assert.DoesNotContain(plan.Mounts, m => m.Kind is PlanMountKind.Dev or PlanMountKind.Proc or PlanMountKind.Tmpfs);
        Assert.DoesNotContain(plan.Mounts, m => m.Path.StartsWith("/run", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void Tmp_becomes_the_run_areas_tmp_in_the_mounts_and_the_environment()
    {
        var plan = Build(Req.WriteScope);

        Assert.Equal(PlanMountKind.ReadWrite, plan.Mounts.Last(m => m.Path == RunTmp).Kind);
        Assert.DoesNotContain(plan.Mounts, m => string.Equals(m.Path, "/tmp", StringComparison.Ordinal));
        Assert.Equal(RunTmp, Env(plan, "TMPDIR"));
        Assert.Equal(RunTmp + "/dotnet-home", Env(plan, "DOTNET_CLI_HOME"));
        Assert.Equal(RunTmp + "/nuget-http", Env(plan, "NUGET_HTTP_CACHE_PATH"));
        Assert.Equal(RunTmp + "/npm-logs", Env(plan, "npm_config_logs_dir"));
        Assert.DoesNotContain(plan.Environment, e => e.Value is { } v && (string.Equals(v, "/tmp", StringComparison.Ordinal) || v.StartsWith("/tmp/", StringComparison.Ordinal)));
    }

    [Fact, Trait("Lane", "unit")]
    public void Bubblewrap_keeps_tmp_and_home_as_they_were()
    {
        var plan = Build(Req.WriteScope, SandboxMechanism.Bubblewrap);

        Assert.Equal("/tmp", Env(plan, "TMPDIR"));
        Assert.Equal(Req.Home, Env(plan, "HOME"));
    }

    [Fact, Trait("Lane", "unit")]
    public void Home_is_a_per_launch_folder_with_the_real_state_directory_named()
    {
        var plan = Build(Req.WriteScope);

        Assert.Equal(RunHome, Env(plan, "HOME"));
        Assert.Equal(PlanMountKind.PerLaunchDir, plan.Mounts.Last(m => m.Path == RunHome).Kind);
        Assert.Equal(Req.StateDir, Env(plan, "AW_STATE_DIR"));   // the hooks default to $HOME/.agentic-workflow
        Assert.Equal(Req.Home + "/.nuget/packages", Env(plan, "NUGET_PACKAGES"));
        Assert.Equal(Req.Home + "/.dotnet", Env(plan, "DOTNET_ROOT"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_profile_declared_home_entry_is_a_link_in_the_run_home_and_a_rule_on_the_real_path()
    {
        var settings = new SandboxSettings(Fs: new SandboxFs(HomeAllow: [new HomeAllowEntry(".venv", MountMode.ReadWrite)]));
        var plan = Build(Req.WriteScope with { Settings = settings });

        var link = plan.Mounts.Single(m => m.Kind == PlanMountKind.Link && m.Path == RunHome + "/.venv");
        Assert.Equal(Req.Home + "/.venv", link.Source);
        Assert.Equal(PlanMountKind.ReadWrite, plan.Mounts.Last(m => m.Path == Req.Home + "/.venv" && m.Kind != PlanMountKind.Link).Kind);
        Assert.Contains(plan.Mounts, m => m.Kind == PlanMountKind.Link && m.Path == RunHome + "/.local/bin" && m.Source == Req.Home + "/.local/bin");
    }

    [Fact, Trait("Lane", "unit")]
    public void The_real_home_and_sindris_home_are_denied_before_their_allowed_entries()
    {
        var plan = Build(Req.WriteScope);

        var homeDeny = IndexOf(plan, Req.Home, PlanMountKind.Deny);
        Assert.True(homeDeny >= 0);
        Assert.True(IndexOf(plan, Req.Home + "/.local/bin", PlanMountKind.ReadOnly) > homeDeny);
        var sindriDeny = IndexOf(plan, Req.SindriHome, PlanMountKind.Deny);
        Assert.True(sindriDeny >= 0);
        Assert.True(IndexOf(plan, Req.SindriHome + "/launches", PlanMountKind.ReadWrite) > sindriDeny);
        Assert.True(IndexOf(plan, Req.SindriHome + "/approved", PlanMountKind.ReadOnly) > sindriDeny);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_run_areas_own_folders_come_back_after_the_deny_that_covers_them()
    {
        var plan = Build(Req.WriteScope);
        var lastCoveringDeny = plan.Mounts.ToList().FindLastIndex(m => m.Kind == PlanMountKind.Deny && (Req.RunArea + "/").StartsWith(m.Path + "/", StringComparison.Ordinal));

        Assert.True(IndexOf(plan, RunTmp, PlanMountKind.ReadWrite) > lastCoveringDeny);
        Assert.True(IndexOf(plan, RunHome, PlanMountKind.PerLaunchDir) > lastCoveringDeny);
        Assert.True(IndexOf(plan, Req.RunArea + "/spool/logs", PlanMountKind.PerLaunchDir) > lastCoveringDeny);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_session_homes_control_file_is_a_link_to_the_real_endpoint_file()
    {
        var plan = Build(Req.WriteScope);
        var session = Req.SindriHome + "-sessions/" + Req.WriteScope.Launch.Value;

        var link = plan.Mounts.Single(m => m.Kind == PlanMountKind.Link && m.Path == session + "/control.json");
        Assert.Equal(Req.SindriHome + "/control.json", link.Source);
        Assert.Equal(PlanMountKind.ReadOnly, plan.Mounts.Last(m => m.Path == Req.SindriHome + "/control.json" && m.Kind != PlanMountKind.Link).Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_owner_copy_transcripts_are_a_link_in_the_config_directory_and_a_rule_on_the_owners_folder()
    {
        var plan = Build(Req.WriteScope);
        var config = Req.RunArea + "/login";
        var real = $"{Req.Home}/.claude/projects/{ProjectKey.For(Req.Worktree)}";

        Assert.Equal(config, Env(plan, "CLAUDE_CONFIG_DIR"));
        var link = plan.Mounts.Single(m => m.Kind == PlanMountKind.Link && m.Path.StartsWith(config + "/projects/", StringComparison.Ordinal));
        Assert.Equal(real, link.Source);
        Assert.Equal(PlanMountKind.ReadWrite, plan.Mounts.Last(m => m.Path == real && m.Kind != PlanMountKind.Link).Kind);
        Assert.Equal(PlanMountKind.ReadOnly, plan.Mounts.Last(m => m.Path == real + "/memory").Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_installed_binaries_link_stays_where_the_wrappers_exec()
    {
        var plan = Build(Req.WriteScope);

        var current = plan.Mounts.Single(m => m.Path == Req.StateDir + "/bin/cs/current");
        Assert.Equal(PlanMountKind.ReadOnly, current.Kind);
        Assert.Null(current.Source);
        Assert.Contains(plan.Mounts, m => m.Path == Req.CsBinDir && m.Kind == PlanMountKind.ReadOnly);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_owners_denies_stay_the_last_entries()
    {
        var plan = Build(Req.WriteScope with { ExtraDeny = [Req.Worktree + "/secrets"] });

        Assert.Equal(new PlanMount(Req.Worktree + "/secrets", PlanMountKind.Deny, Reason: "blocked by the line's capability policy"), plan.Mounts[^1]);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_inner_sandbox_is_off_on_seatbelt_whatever_the_profile_says()
    {
        var plan = Build(Req.WriteScope with { Settings = new SandboxSettings(Inner: InnerSandbox.On) });

        Assert.Equal(InnerSandbox.Off, plan.Inner);
        Assert.Equal(InnerSandbox.On, Build(Req.WriteScope with { Settings = new SandboxSettings(Inner: InnerSandbox.On) }, SandboxMechanism.Bubblewrap).Inner);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_same_request_hashes_the_same_on_seatbelt_and_differs_from_bubblewrap()
    {
        Assert.Equal(Build(Req.WriteScope).PlanHash, Build(Req.WriteScope).PlanHash, StringComparer.Ordinal);
        Assert.NotEqual(Build(Req.WriteScope).PlanHash, Build(Req.WriteScope, SandboxMechanism.Bubblewrap).PlanHash, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_bubblewrap_plan_is_the_pass_through_plan_and_has_no_link()
    {
        var bubblewrap = Build(Req.WriteScope, SandboxMechanism.Bubblewrap);
        var passThrough = Build(Req.WriteScope, SandboxMechanism.PassThrough);

        Assert.True(passThrough.Mounts.SequenceEqual(bubblewrap.Mounts));
        Assert.True(passThrough.Environment.SequenceEqual(bubblewrap.Environment));
        Assert.DoesNotContain(bubblewrap.Mounts, m => m.Kind == PlanMountKind.Link);
        Assert.Contains(bubblewrap.Mounts, m => m.Kind == PlanMountKind.Tmpfs && m.Path == Req.Home);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_attended_seatbelt_plan_builds_the_same_way()
    {
        var plan = Build(Req.Attended);

        Assert.Equal(RunHome, Env(plan, "HOME"));
        Assert.Equal(InnerSandbox.Off, plan.Inner);
    }
}
