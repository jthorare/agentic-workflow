using Aw.Sindri.Core.Sandbox.Plan;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>The two additions the bubblewrap adapter needs from the plan (spike item 1, notes 1 and 3): a /run tmpfs and the state directory the hooks look in.</summary>
public sealed class SandboxPlanRuntimeDirectoryTests
{
    [Fact, Trait("Lane", "unit")]
    public void Run_is_a_small_tmpfs_before_the_net_directory_is_bound()
    {
        var plan = SandboxPlannerTests.Build(Req.WriteScope);

        var run = plan.Mounts.ToList().FindIndex(m => m.Path == "/run");
        var net = plan.Mounts.ToList().FindIndex(m => m.Path == "/run/sindri/net");
        Assert.True(run >= 0 && run < net);
        Assert.Equal(PlanMountKind.Tmpfs, plan.Mounts[run].Kind);
        Assert.True(plan.Mounts[run].Size is > 0 and <= 64L * 1024 * 1024);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_default_state_directory_is_not_in_the_environment() =>
        Assert.DoesNotContain("AW_STATE_DIR", SandboxPlannerTests.Build(Req.WriteScope).Environment.Select(e => e.Name), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void A_state_directory_elsewhere_is_in_the_environment_so_the_hooks_find_it()
    {
        var host = Req.Host with { StateDir = "/srv/aw", SindriHome = "/srv/aw/sindri-app" };

        var plan = SandboxPlannerTests.Build(Req.WriteScope with
        {
            Host = host,
            Paths = Req.WriteScope.Paths with { RunDirectory = "/srv/aw/sindri-app/runs/x/s/1", RunArea = "/srv/aw/sindri-app/areas/run-1" },
        });

        Assert.Equal("/srv/aw", plan.Environment.Single(e => e.Name == "AW_STATE_DIR").Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_transcript_directory_of_the_worktree_is_the_only_agent_home_entry_bound_into_the_config_directory()
    {
        var plan = SandboxPlannerTests.Build(Req.WriteScope);

        var key = "-work-web--claude-worktrees-sindri-x-1";
        var projects = plan.Mounts.Where(m => m.Path.StartsWith(Req.Home + "/.claude-run/projects", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, projects.Count);
        Assert.Equal(new PlanMount($"{Req.Home}/.claude-run/projects/{key}", PlanMountKind.ReadWrite, $"{Req.Home}/.claude/projects/{key}", Reason: projects[0].Reason), projects[0]);
        Assert.Equal((PlanMountKind.ReadOnly, $"{Req.Home}/.claude/projects/{key}/memory"), (projects[1].Kind, projects[1].Source));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("/work/web/.claude/worktrees/sindri-x-1", "-work-web--claude-worktrees-sindri-x-1")]
    [InlineData("/home/thor/repos/agentic-workflow", "-home-thor-repos-agentic-workflow")]
    [InlineData("/a b/c_d", "-a-b-c-d")]
    public void The_project_key_is_the_path_with_every_non_alphanumeric_character_a_dash(string path, string key) =>
        Assert.Equal(key, ProjectKey.For(path));

    [Fact, Trait("Lane", "unit")]
    public void A_read_only_step_without_a_worktree_binds_no_transcript_directory() =>
        Assert.DoesNotContain(SandboxPlannerTests.Build(Req.ReadOnly with { Paths = Req.ReadOnly.Paths with { Worktree = null } }).Mounts, m => m.Path.Contains("/projects/", StringComparison.Ordinal));
}
