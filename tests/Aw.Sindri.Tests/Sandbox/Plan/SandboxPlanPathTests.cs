using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>#355 run 1, B1 and B2: the builder never produces a looser plan than the engine asked for, and never binds a path it did not check (an empty string must not widen a bind).</summary>
public sealed class SandboxPlanPathTests
{
    private const string SindriHome = Req.SindriHome;

    private static void Refused(SandboxRequest request)
    {
        var result = SandboxPlanner.Build(SandboxMechanism.PassThrough, request);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.LaunchUnconfinable.Code, result.Error.Code);
        Assert.Equal("worktree-outside-repo", SandboxRefusal.ReasonOf(result.Error));
    }

    private static SandboxRequest WithPaths(Func<SandboxPaths, SandboxPaths> change) => Req.WriteScope with { Paths = change(Req.WriteScope.Paths) };

    [Theory, Trait("Lane", "unit")]
    [InlineData(StepPermissionMode.ReadOnly)]
    [InlineData(StepPermissionMode.Plan)]
    public void A_read_only_step_with_a_writable_path_is_refused(StepPermissionMode mode) =>
        Refused(Req.WriteScope with { Policy = new StepPolicy([], mode, Req.Worktree) });

    [Fact, Trait("Lane", "unit")]
    public void Plan_mode_is_never_writable()
    {
        var plan = SandboxPlanner.Build(SandboxMechanism.PassThrough, Req.WriteScope with { Policy = new StepPolicy([], StepPermissionMode.Plan, WritablePath: null) }).Value;

        Assert.DoesNotContain(plan.Mounts, m => m.Kind == PlanMountKind.ReadWrite && m.Path.StartsWith(Req.Repo, StringComparison.Ordinal));
        Assert.DoesNotContain("packages", plan.Network.Groups.Select(g => g.Name), StringComparer.Ordinal);
        Assert.Contains(plan.Policy.Rules, r => r.Effect == InnerRuleEffect.Allow && r.Tool == "Bash" && r.Pattern == "git status:*");
        Assert.DoesNotContain(plan.Policy.Rules, r => r.Effect == InnerRuleEffect.Allow && r.Tool == "Bash" && r.Pattern.Length == 0);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("sindri")]
    [InlineData("sindri/x")]
    [InlineData("/sindri/x/1")]
    [InlineData("sindri/x/1/")]
    [InlineData("sindri/../1/x")]
    [InlineData("sindri/./x/1")]
    [InlineData("sindri//x/1")]
    [InlineData("other/x/1")]
    [InlineData("sindri/x/1.lock")]
    [InlineData("sindri/x y/1")]
    public void A_bad_run_namespace_is_refused_and_never_binds_refs_heads(string ns) => Refused(Req.WriteScope with { RunNamespace = ns });

    [Theory, Trait("Lane", "unit")]
    [InlineData("/work/web/.claude/worktrees/sindri-x-1/")]
    [InlineData("/work/web/../etc/x")]
    [InlineData("/work/web/.claude/../../etc/x")]
    [InlineData("/work/web/./wt")]
    [InlineData("work/web/wt")]
    [InlineData("")]
    [InlineData("/work/web")]
    [InlineData("/work/webother/wt")]
    [InlineData("/work/web//wt")]
    public void A_bad_worktree_is_refused_and_never_binds_the_worktrees_directory(string worktree) =>
        Refused(Req.WriteScope with { Paths = Req.WriteScope.Paths with { Worktree = worktree }, Policy = new StepPolicy([], StepPermissionMode.Default, worktree) });

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("runs/1")]
    [InlineData("/elsewhere/runs/1")]
    [InlineData(SindriHome + "/workflow/../../x")]
    [InlineData(SindriHome + "/workflow/runs/1/")]
    [InlineData(SindriHome)]
    public void A_bad_run_directory_or_run_area_is_refused(string path)
    {
        Refused(WithPaths(p => p with { RunDirectory = path }));
        Refused(WithPaths(p => p with { RunArea = path }));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData(".git")]
    [InlineData("/work/web/../.git")]
    [InlineData("/work/web/.git/")]
    public void A_bad_git_directory_is_refused(string git) => Refused(WithPaths(p => p with { GitCommonDir = git }));

    [Fact, Trait("Lane", "unit")]
    public void A_bad_home_state_or_net_directory_is_refused()
    {
        Refused(Req.WriteScope with { Host = Req.Host with { Home = string.Empty } });
        Refused(Req.WriteScope with { Host = Req.Host with { StateDir = "state/" } });
        Refused(Req.WriteScope with { Net = Req.WriteScope.Net with { NetDir = string.Empty } });
    }

    [Fact, Trait("Lane", "unit")]
    public void The_good_paths_still_bind_only_the_run_ref_directory_and_its_own_worktree_state()
    {
        var plan = SandboxPlannerTests.Build(Req.WriteScope);

        Assert.DoesNotContain(plan.Mounts, m => m.Kind == PlanMountKind.ReadWrite && (m.Path.EndsWith("/refs/heads", StringComparison.Ordinal) || m.Path.EndsWith("/logs/refs/heads", StringComparison.Ordinal) || m.Path.EndsWith("/.git/worktrees", StringComparison.Ordinal)));
    }
}
