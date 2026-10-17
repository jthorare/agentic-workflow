using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch.Forms;

/// <summary>The run environment of an unattended Step's session (L-17(b)).</summary>
public sealed class StepEnvironmentTests
{
    private static LaunchRequest Request(StepArgs? step, string? worktree = "/work/repo/.claude/worktrees/sindri-1") =>
        new(LaunchId.For(Guid.Empty), A.FirstItemId, AgentId.From("claude-code"), LaunchMode.Open, Attended: step is null, "/work/repo", InstanceAddress.From("ITM-1/spec/1"), worktree, step, Run: null);

    [Fact, Trait("Lane", "unit")]
    public void Host_env_carries_AW_SINDRI_INSTANCE_and_AW_SINDRI_RUN_DIR_on_a_step()
    {
        var env = StepEnvironment.For(Request(new StepArgs(Model: null, Policy: null, new StepRef("build", "agent"))));

        Assert.Equal("ITM-1/spec/1", env["AW_SINDRI_INSTANCE"]);
        Assert.Equal("/work/repo/.claude/worktrees/sindri-1", env["AW_SINDRI_RUN_DIR"]);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_policys_writable_path_is_the_run_dir_when_it_names_one()
    {
        var policy = new StepPolicy([], StepPermissionMode.ReadOnly, "/runs/ITM-1/step");

        var env = StepEnvironment.For(Request(new StepArgs(Model: null, policy, new StepRef("build", "agent"))));

        Assert.Equal("/runs/ITM-1/step", env["AW_SINDRI_RUN_DIR"]);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_human_open_gets_neither_variable() =>
        Assert.Empty(StepEnvironment.For(Request(step: null)));

    [Fact, Trait("Lane", "unit")]
    public void A_step_without_a_directory_gets_the_instance_only()
    {
        var env = StepEnvironment.For(Request(new StepArgs(Model: null, Policy: null, new StepRef("build", "agent")), worktree: null));

        Assert.Equal(["AW_SINDRI_INSTANCE"], env.Keys, StringComparer.Ordinal);
    }
}
