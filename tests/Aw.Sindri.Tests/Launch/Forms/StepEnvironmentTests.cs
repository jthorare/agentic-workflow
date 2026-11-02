using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch.Forms;

/// <summary>The run environment of an unattended Step's session (L-17(b)); without a Sindri home the run directory is the worktree or the policy's path.</summary>
public sealed class StepEnvironmentTests
{
    private static LaunchRequest Request(StepArgs? step, string? worktree = "/work/repo/.claude/worktrees/sindri-1") =>
        new(LaunchId.For(Guid.Empty), A.FirstItemId, AgentId.From("claude-code"), LaunchMode.Open, Attended: step is null, "/work/repo", InstanceAddress.From("ITM-1/spec/1"), worktree, step, Run: null);

    [Fact, Trait("Lane", "unit")]
    public void Host_env_carries_AW_SINDRI_INSTANCE_and_AW_SINDRI_RUN_DIR_on_a_step()
    {
        var env = StepEnvironment.For(sindriHome: null, request: Request(new StepArgs(Model: null, Policy: null, new StepRef("build", "agent"))));

        Assert.Equal("ITM-1/spec/1", env["AW_SINDRI_INSTANCE"]);
        Assert.Equal("/work/repo/.claude/worktrees/sindri-1", env["AW_SINDRI_RUN_DIR"]);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_policys_writable_path_is_the_run_dir_when_it_names_one()
    {
        var policy = new StepPolicy([], StepPermissionMode.ReadOnly, "/runs/ITM-1/step");

        var env = StepEnvironment.For(sindriHome: null, request: Request(new StepArgs(Model: null, policy, new StepRef("build", "agent"))));

        Assert.Equal("/runs/ITM-1/step", env["AW_SINDRI_RUN_DIR"]);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_human_open_gets_neither_variable() =>
        Assert.Empty(StepEnvironment.For(sindriHome: null, request: Request(step: null)));

    [Fact, Trait("Lane", "unit")]
    public void A_step_without_a_directory_gets_the_instance_only()
    {
        var env = StepEnvironment.For(sindriHome: null, request: Request(new StepArgs(Model: null, Policy: null, new StepRef("build", "agent")), worktree: null));

        Assert.Equal(["AW_SINDRI_INSTANCE"], env.Keys, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void With_a_Sindri_home_the_run_dir_is_the_launchs_own_folder_under_workflow_runs()
    {
        var request = Request(new StepArgs(Model: null, new StepPolicy([], StepPermissionMode.ReadOnly, "/runs/ITM-1/step"), new StepRef("build", "agent")));

        var env = StepEnvironment.For(request, "/home/owner/sindri");

        Assert.Equal(
            $"/home/owner/sindri/workflow/runs/{WorkflowInstanceId.Of(InstanceAddress.From("ITM-1/spec/1")).Value}/build/{request.Launch.Value}",
            env["AW_SINDRI_RUN_DIR"]);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_human_open_has_no_run_dir_even_with_a_Sindri_home() =>
        Assert.Null(StepEnvironment.RunDirectoryOf(Request(step: null), "/home/owner/sindri"));
}
