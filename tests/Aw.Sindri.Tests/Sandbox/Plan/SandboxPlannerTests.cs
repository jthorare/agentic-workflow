using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Sandbox.Plan;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>The pure plan builder: the mount stack and the deny lists (sandbox spec §3.1, §5).</summary>
public sealed class SandboxPlannerTests
{
    internal static SandboxPlan Build(SandboxRequest request)
    {
        var result = SandboxPlanner.Build(SandboxMechanism.PassThrough, request);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    private static PlanMount? Last(SandboxPlan plan, string path) => plan.Mounts.LastOrDefault(m => string.Equals(m.Path, path, StringComparison.Ordinal));

    private static int IndexOf(SandboxPlan plan, string path, PlanMountKind kind) =>
        plan.Mounts.ToList().FindLastIndex(m => string.Equals(m.Path, path, StringComparison.Ordinal) && m.Kind == kind);

    [Fact, Trait("Lane", "unit")]
    public void ReadOnly_makes_only_the_run_directory_writable()
    {
        var plan = Build(Req.ReadOnly);

        Assert.Contains(Req.RunDirectory, plan.Writable, StringComparer.Ordinal);
        Assert.Contains("/tmp", plan.Writable, StringComparer.Ordinal);
        Assert.DoesNotContain(plan.Writable, p => p.StartsWith(Req.Repo, StringComparison.Ordinal));
        Assert.NotEqual(PlanMountKind.ReadWrite, Last(plan, Req.Worktree)?.Kind ?? PlanMountKind.ReadOnly);
    }

    [Fact, Trait("Lane", "unit")]
    public void WriteScope_adds_the_worktree_and_the_run_ref_directory()
    {
        var plan = Build(Req.WriteScope);

        Assert.Equal(PlanMountKind.ReadWrite, Last(plan, Req.Worktree)!.Kind);
        Assert.Equal(PlanMountKind.ReadWrite, Last(plan, "/work/web/.git/refs/heads/sindri/x/1")!.Kind);
        Assert.Equal(PlanMountKind.ReadWrite, Last(plan, "/work/web/.git/logs/refs/heads/sindri/x/1")!.Kind);
        Assert.Equal(PlanMountKind.ReadWrite, Last(plan, "/work/web/.git/objects")!.Kind);
        Assert.Equal(PlanMountKind.ReadWrite, Last(plan, "/work/web/.git/worktrees/sindri-x-1")!.Kind);
        Assert.Equal(PlanMountKind.ReadOnly, Last(plan, "/work/web/.git")!.Kind);
        Assert.Contains(Req.RunDirectory, plan.Writable, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Run_branch_is_inside_its_own_directory()
    {
        var plan = Build(Req.WriteScope);
        var writableGit = plan.Mounts.Where(m => m.Kind == PlanMountKind.ReadWrite && m.Path.StartsWith("/work/web/.git/", StringComparison.Ordinal)).Select(m => m.Path).ToList();

        Assert.DoesNotContain("/work/web/.git/refs/heads", writableGit, StringComparer.Ordinal);
        Assert.DoesNotContain("/work/web/.git/refs", writableGit, StringComparer.Ordinal);
        Assert.DoesNotContain(writableGit, p => p.EndsWith("/config", StringComparison.Ordinal) || p.Contains("/hooks", StringComparison.Ordinal) || p.EndsWith("packed-refs", StringComparison.Ordinal));
        Assert.Equal(
            ["/work/web/.git/logs/refs/heads/sindri/x/1", "/work/web/.git/objects", "/work/web/.git/refs/heads/sindri/x/1", "/work/web/.git/worktrees/sindri-x-1"],
            [.. writableGit.Order(StringComparer.Ordinal)]);
    }

    [Fact, Trait("Lane", "unit")]
    public void Home_is_empty_unless_allowlisted()
    {
        var plan = Build(Req.WriteScope);
        var home = Last(plan, Req.Home)!;

        Assert.Equal(PlanMountKind.Tmpfs, home.Kind);
        Assert.Equal(256L * 1024 * 1024, home.Size);
        Assert.Null(Last(plan, Req.Home + "/.ssh"));
        Assert.Null(Last(plan, Req.Home + "/.gitconfig"));
        Assert.Null(Last(plan, Req.Home + "/.config"));
        foreach (var ro in new[] { ".dotnet", ".nvm", ".cargo", ".rustup", ".local/bin" })
        {
            Assert.Equal(PlanMountKind.ReadOnly, Last(plan, $"{Req.Home}/{ro}")!.Kind);
        }

        Assert.Equal(PlanMountKind.ReadWrite, Last(plan, Req.Home + "/.nuget/packages")!.Kind);
        Assert.Equal(PlanMountKind.ReadWrite, Last(plan, Req.Home + "/.npm/_cacache")!.Kind);
        Assert.Equal(PlanMountKind.ReadOnly, Last(plan, Req.Host.ToolkitDir)!.Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_declared_home_entry_and_a_repo_cache_path_are_bound_and_deny_extra_wins()
    {
        var settings = new SandboxSettings(
            Fs: new SandboxFs(HomeAllow: [new HomeAllowEntry(".config/gh", MountMode.ReadOnly)], DenyExtra: [Req.Home + "/.config/gh"]),
            Repos: new Dictionary<string, SandboxRepoSettings>(StringComparer.Ordinal) { ["web"] = new(CachePaths: [".cache/pip"], HomeAllow: [new HomeAllowEntry(".venv", MountMode.ReadWrite)]) });

        var plan = Build(Req.WriteScope with { Settings = settings });

        Assert.Equal(PlanMountKind.ReadWrite, Last(plan, Req.Home + "/.cache/pip")!.Kind);
        Assert.Equal(PlanMountKind.ReadWrite, Last(plan, Req.Home + "/.venv")!.Kind);
        Assert.Equal(PlanMountKind.Deny, Last(plan, Req.Home + "/.config/gh")!.Kind);   // the allow is still earlier in the stack, the deny is the last word
        Assert.True(IndexOf(plan, Req.Home + "/.config/gh", PlanMountKind.Deny) > IndexOf(plan, Req.Home + "/.config/gh", PlanMountKind.ReadOnly));
    }

    [Fact, Trait("Lane", "unit")]
    public void Agent_home_config_is_read_only_and_the_original_login_is_masked_inside_it()
    {
        var plan = Build(Req.WriteScope);

        Assert.Equal(PlanMountKind.ReadOnly, Last(plan, Req.Home + "/.claude")!.Kind);
        Assert.Equal(PlanMountKind.Deny, Last(plan, Req.Home + "/.claude/.credentials.json")!.Kind);
        Assert.True(IndexOf(plan, Req.Home + "/.claude/.credentials.json", PlanMountKind.Deny) > IndexOf(plan, Req.Home + "/.claude", PlanMountKind.ReadOnly));
        Assert.Equal(PlanMountKind.ReadWrite, Last(plan, Req.Home + "/.claude-run")!.Kind);
        Assert.Null(Last(plan, Req.Home + "/.codex/auth.json"));   // no codex home was bound
    }

    [Fact, Trait("Lane", "unit")]
    public void A_login_mount_of_a_rename_written_file_is_refused_because_a_file_bind_cannot_be_renamed_onto()
    {
        var login = Req.OwnerCopyLogin() with { Mounts = [new LoginMount(Req.RunArea + "/c.json", Req.Home + "/.claude-run/.credentials.json", MountMode.ReadWrite)] };

        var result = SandboxPlanner.Build(SandboxMechanism.PassThrough, Req.WriteScope with { Login = login });

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.LaunchSelfCheckFailed.Code, result.Error.Code);
        Assert.Equal("login", SandboxRefusal.ReasonOf(result.Error));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_writable_agent_home_root_is_refused()
    {
        var login = Req.OwnerCopyLogin() with { Mounts = [new LoginMount(Req.Home + "/.claude", Req.Home + "/.claude", MountMode.ReadWrite)] };

        var result = SandboxPlanner.Build(SandboxMechanism.PassThrough, Req.WriteScope with { Login = login });

        Assert.Equal("login", SandboxRefusal.ReasonOf(result.Error));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_login_from_another_source_than_the_resolved_one_is_refused()
    {
        var login = Req.OwnerCopyLogin() with { Source = ModelLoginSource.SeparateLogin };

        var result = SandboxPlanner.Build(SandboxMechanism.PassThrough, Req.WriteScope with { Login = login });

        Assert.Equal(SindriErrors.LaunchSelfCheckFailed.Code, result.Error.Code);
        Assert.Equal("login", SandboxRefusal.ReasonOf(result.Error));
    }

    [Fact, Trait("Lane", "unit")]
    public void State_dir_is_denied_except_the_hook_inventory()
    {
        var plan = Build(Req.WriteScope);
        var denyAt = IndexOf(plan, Req.StateDir, PlanMountKind.Deny);

        Assert.True(denyAt >= 0);
        Assert.True(IndexOf(plan, Req.SindriHome, PlanMountKind.Deny) >= 0);
        var kindOf = new Dictionary<StatePathKind, PlanMountKind>
        {
            [StatePathKind.ReadOnly] = PlanMountKind.ReadOnly,
            [StatePathKind.ReadWrite] = PlanMountKind.ReadWrite,
            [StatePathKind.PerLaunchDir] = PlanMountKind.PerLaunchDir,
        };
        foreach (var entry in HookInventory.StatePaths.Where(e => e.Kind != StatePathKind.Absent))
        {
            var path = $"{Req.StateDir}/{entry.Path}";
            Assert.True(IndexOf(plan, path, kindOf[entry.Kind]) > denyAt, path);
        }

        foreach (var entry in HookInventory.StatePaths.Where(e => e.Kind == StatePathKind.Absent))
        {
            Assert.DoesNotContain(plan.Mounts, m => string.Equals(m.Path, $"{Req.StateDir}/{entry.Path}", StringComparison.Ordinal) && m.Kind != PlanMountKind.Deny);
        }

        Assert.Equal(PlanMountKind.ReadOnly, Last(plan, Req.StateDir + "/judge/config.json")!.Kind);
        Assert.Equal(PlanMountKind.PerLaunchDir, Last(plan, Req.SindriHome + "/launches")!.Kind);
        var afterDeny = plan.Mounts.Skip(denyAt + 1).Where(m => m.Path.StartsWith(Req.StateDir + "/", StringComparison.Ordinal) && m.Kind != PlanMountKind.Deny).Select(m => m.Path).ToList();
        Assert.All(afterDeny, p => Assert.True(
            HookInventory.StatePaths.Any(e => p.StartsWith($"{Req.StateDir}/{e.Path}", StringComparison.Ordinal)) || p.StartsWith(Req.SindriHome + "/", StringComparison.Ordinal), p));
    }

    [Fact, Trait("Lane", "unit")]
    public void Docker_socket_is_always_denied_with_the_other_permanent_denies()
    {
        var plan = Build(Req.WriteScope);

        foreach (var path in new[] { "/var/run/docker.sock", "/run/podman", "/run/user/1000", "/etc/ssh", "/root", "/home/other" })
        {
            Assert.Equal(PlanMountKind.Deny, Last(plan, path)!.Kind);
        }

        Assert.Null(Last(plan, Req.Home + "/Library/Keychains"));
        var mac = Build(Req.WriteScope with { Host = Req.Host with { IsMacOs = true } });
        Assert.Equal(PlanMountKind.Deny, Last(mac, Req.Home + "/Library/Keychains")!.Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_Step_cannot_widen_the_profile_floor()
    {
        var outside = SandboxPlanner.Build(SandboxMechanism.PassThrough, Req.WriteScope with { Policy = new StepPolicy([], StepPermissionMode.Default, Req.StateDir) });
        var elsewhere = SandboxPlanner.Build(SandboxMechanism.PassThrough, Req.WriteScope with { Policy = new StepPolicy([], StepPermissionMode.Default, "/work/other") });
        var strayWorktree = SandboxPlanner.Build(SandboxMechanism.PassThrough, Req.WriteScope with { Paths = Req.WriteScope.Paths with { Worktree = "/elsewhere/wt" }, Policy = new StepPolicy([], StepPermissionMode.Default, "/elsewhere/wt") });

        foreach (var result in new[] { outside, elsewhere, strayWorktree })
        {
            Assert.True(result.IsError);
            Assert.Equal(SindriErrors.LaunchUnconfinable.Code, result.Error.Code);
            Assert.Equal("worktree-outside-repo", SandboxRefusal.ReasonOf(result.Error));
        }

        var denied = Build(Req.WriteScope with { ExtraDeny = [Req.Worktree + "/holdout"] });
        Assert.Equal(PlanMountKind.Deny, denied.Mounts[^1].Kind);
        Assert.Equal(Req.Worktree + "/holdout", denied.Mounts[^1].Path);
    }

    [Fact, Trait("Lane", "unit")]
    public void Missing_sandbox_section_uses_defaults()
    {
        var plan = Build(Req.WriteScope);
        var readOnly = Build(Req.ReadOnly);

        Assert.Equal(NetworkMode.Enforce, plan.Network.Mode);
        Assert.Equal(InnerSandbox.Auto, plan.Inner);
        Assert.Equal(GithubCredentials.None, plan.Github);
        Assert.Equal(ModelLoginSource.OwnerCopy, plan.Login);
        Assert.Equal(["model", "packages"], plan.Network.Groups.Select(g => g.Name), StringComparer.Ordinal);
        Assert.Equal(["model"], readOnly.Network.Groups.Select(g => g.Name), StringComparer.Ordinal);
        Assert.Equal(3, plan.Limits.Sessions);
        Assert.Equal(2048, plan.Limits.TasksMax);
        Assert.Equal(400, plan.Limits.CpuQuotaPercent);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_summary_carries_the_plan_and_no_values()
    {
        var plan = Build(Req.WriteScope);

        var summary = plan.ToSummary();

        Assert.Equal(plan.PlanHash, summary.PlanHash);
        Assert.Equal(SandboxMechanism.PassThrough, summary.Mechanism);
        Assert.Equal(plan.Writable, summary.Writable);
        Assert.Equal(["model", "packages"], summary.Network);
        Assert.Equal("70m", summary.Limits.Wall);
        Assert.Equal("400%", summary.Limits.CpuQuota);
        Assert.False(summary.Attended);
        Assert.Equal(ModelLoginSource.OwnerCopy, summary.Login);
    }

    [Fact, Trait("Lane", "unit")]
    public void Socket_path_is_under_the_limit()
    {
        var plan = Build(Req.WriteScope);
        var tooLong = SandboxPlanner.Build(SandboxMechanism.PassThrough, Req.WriteScope with { Net = Req.WriteScope.Net with { NetDir = "/tmp/" + new string('n', 110) } });

        Assert.True(System.Text.Encoding.UTF8.GetByteCount(plan.Network.NetDir + "/proxy.sock") < 104);
        Assert.True(tooLong.IsError);
        Assert.Equal(SindriErrors.LaunchSelfCheckFailed.Code, tooLong.Error.Code);
        Assert.Equal("net-forward", SandboxRefusal.ReasonOf(tooLong.Error));
    }

    [Fact, Trait("Lane", "unit")]
    public void Resume_of_a_step_session_is_confined()
    {
        var plan = Build(Req.WriteScope);

        var reused = SandboxPlanner.FromStored(plan);
        var gone = SandboxPlanner.FromStored(stored: null);

        Assert.Same(plan, reused.Value);
        Assert.Equal(Build(Req.WriteScope).PlanHash, plan.PlanHash);
        Assert.Equal(SindriErrors.LaunchUnconfinable.Code, gone.Error.Code);
        Assert.Equal("no-stored-plan", SandboxRefusal.ReasonOf(gone.Error));
    }

    [Fact, Trait("Lane", "unit")]
    public void Resume_of_an_attended_session_is_confined()
    {
        var plan = Build(Req.Attended);

        Assert.Same(plan, SandboxPlanner.FromStored(plan).Value);
        Assert.Equal("no-stored-plan", SandboxRefusal.ReasonOf(SandboxPlanner.FromStored(stored: null).Error));
    }
}
