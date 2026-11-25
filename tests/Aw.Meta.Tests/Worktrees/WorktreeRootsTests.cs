using Aw.Meta.Core.Config;
using Aw.Meta.Core.Worktrees;

namespace Aw.Meta.Tests.Worktrees;

public sealed class WorktreeRootsTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_path_strictly_inside_a_root_is_allowed_the_root_itself_and_its_siblings_are_not()
    {
        string[] roots = ["/home/u/repos/aw-wt", "/home/u/repos/app/.claude/worktrees"];

        Assert.True(WorktreeRoots.Contains(roots, "/home/u/repos/aw-wt/feat-x"));
        Assert.True(WorktreeRoots.Contains(roots, "/home/u/repos/app/.claude/worktrees/agent-1"));
        Assert.False(WorktreeRoots.Contains(roots, "/home/u/repos/aw-wt"));
        Assert.False(WorktreeRoots.Contains(roots, "/home/u/repos/aw-wt2/feat-x"));
        Assert.False(WorktreeRoots.Contains(roots, "/home/u/repos/app"));
    }

    [Fact, Trait("Lane", "unit")]
    public void Dot_dot_segments_and_trailing_slashes_are_normalized_before_comparing()
    {
        string[] roots = ["/home/u/aw-wt/"];

        Assert.True(WorktreeRoots.Contains(roots, "/home/u/aw-wt/a/"));
        Assert.False(WorktreeRoots.Contains(roots, "/home/u/aw-wt/../repos/app"));
        Assert.False(WorktreeRoots.Contains([], "/anything"));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_default_roots_expand_the_home_directory_and_always_include_the_gate_directory()
    {
        var roots = new WorktreeConfig().EffectiveRoots("/home/u/repos/app", "/state/meta/gates", "/home/u", repoRoot: null);

        Assert.Equal(["/home/u/repos/aw-wt", "/home/u/repos/app/.claude/worktrees", "/state/meta/gates"], roots);
    }

    [Fact, Trait("Lane", "unit")]
    public void Configured_roots_replace_the_defaults_but_not_the_gate_directory()
    {
        var roots = new WorktreeConfig(Roots: ["~/trees", "/abs/trees/"]).EffectiveRoots("/r", "/state/meta/gates", "/home/u", repoRoot: null);

        Assert.Equal(["/home/u/trees", "/abs/trees", "/state/meta/gates"], roots);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_default_roots_include_the_repos_own_worktree_root_when_given()
    {
        var roots = new WorktreeConfig().EffectiveRoots("/home/u/repos/app", "/state/meta/gates", "/home/u", repoRoot: "/data/trees/app");

        Assert.Equal(["/home/u/repos/aw-wt", "/home/u/repos/app/.claude/worktrees", "/data/trees/app", "/state/meta/gates"], roots);
    }

    [Fact, Trait("Lane", "unit")]
    public void Configured_roots_ignore_the_repos_own_worktree_root()
    {
        var roots = new WorktreeConfig(Roots: ["/abs/trees"]).EffectiveRoots("/r", "/g", "/home/u", repoRoot: "/data/trees/app");

        Assert.Equal(["/abs/trees", "/g"], roots);
    }

    [Fact, Trait("Lane", "unit")]
    public void Without_a_home_directory_a_tilde_root_is_left_out_rather_than_guessed()
    {
        var roots = new WorktreeConfig().EffectiveRoots("/r", "/g", string.Empty, repoRoot: null);

        Assert.Equal(["/r/.claude/worktrees", "/g"], roots);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_build_outputs_default_to_bin_obj_artifacts_node_modules_and_dist()
    {
        Assert.Equal(["bin", "obj", "artifacts", "node_modules", "dist"], new WorktreeConfig().EffectiveBuildOutputs);
        Assert.Equal(["out"], new WorktreeConfig(BuildOutputs: ["out"]).EffectiveBuildOutputs);
        Assert.True(new WorktreeConfig().Enabled);
    }
}
