using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Testing.Skills;

namespace Aw.Sindri.Tests.Skills;

/// <summary>The environment a skill Step's session gets (skill flows spec §3.3, D8): the result switch, the per-launch result file and the host-computed AW_DIR.</summary>
[Lane(TestLane.Unit)]
public sealed class SkillLaunchEnvTests
{
    private const string RunDirectory = "/home/u/.agentic-workflow/sindri-app/workflow/runs/WFI-1/fix/LCH-1";
    private const string StateDirectory = "/home/u/.agentic-workflow";

    private static readonly LaunchId _launch = LaunchId.From("LCH-" + new string('a', 32));

    [Fact]
    public void A_skill_launch_carries_the_four_variables()
    {
        var env = SkillLaunchEnv.For("review", _launch, RunDirectory, StateDirectory, "acme-api");

        Assert.Equal("1", env["AW_RESULT"]);
        Assert.Equal("review", env["AW_RESULT_SKILL"]);
        Assert.Equal($"{RunDirectory}/.sindri-out/result-{_launch.Value}.json", env["AW_RESULT_FILE"]);
        Assert.Equal("/home/u/.agentic-workflow/acme-api/", env["AW_DIR"]);
        Assert.Equal(4, env.Count);
    }

    [Fact]
    public void The_result_file_is_per_launch_so_a_loops_second_round_never_reads_the_first()
    {
        var second = LaunchId.From("LCH-" + new string('b', 32));

        Assert.NotEqual(SkillLaunchEnv.ResultFileOf(RunDirectory, _launch), SkillLaunchEnv.ResultFileOf(RunDirectory, second), StringComparer.Ordinal);
        Assert.EndsWith($"/.sindri-out/result-{second.Value}.json", SkillLaunchEnv.ResultFileOf(RunDirectory, second), StringComparison.Ordinal);
    }

    [Fact]
    public void The_launch_request_carries_the_skill_and_the_states_timeout()
    {
        var address = InstanceAddress.From("T-3/step/1");
        var run = new AgentStepRun(WorkflowInstanceId.Of(address), address, RunToken.From("2.1"), new StepRef("fix", "step.skill.review"), WriteScope: true);
        var target = new AgentStepTarget(ItemId.From("ITM-" + new string('a', 32)), AgentId.From("claude-code"), "/work/web");
        var skill = new SkillLaunch("review", SkillTarget.Codex, SkillFixtures.Review, new Dictionary<string, string>(StringComparer.Ordinal) { ["pr"] = "42" });

        var request = AgentLaunches.RequestFor(run, target, descriptor: null, new AgentLaunchExtras { Skill = skill, StepTimeout = TimeSpan.FromHours(8) });

        Assert.Equal(skill, request.Skill);
        Assert.Equal(TimeSpan.FromHours(8), request.StepTimeout);
    }

    [Theory]
    [InlineData(true, GithubCredentials.AgentApp)]
    [InlineData(false, null)]
    public void A_skill_Step_asks_for_the_agent_app_only_when_its_block_pushes(bool pushes, GithubCredentials? expected)
    {
        var address = InstanceAddress.From("T-3/step/1");
        var run = new AgentStepRun(WorkflowInstanceId.Of(address), address, RunToken.From("2.1"), new StepRef("fix", "step.skill.x"), WriteScope: true);
        var target = new AgentStepTarget(ItemId.From("ITM-" + new string('a', 32)), AgentId.From("claude-code"), "/work/web");
        var block = pushes ? SkillFixtures.BugFixOrchestrator : SkillFixtures.Review;
        var skill = new SkillLaunch("x", SkillTarget.ClaudeCode, block, new Dictionary<string, string>(StringComparer.Ordinal));

        var request = AgentLaunches.RequestFor(run, target, descriptor: null, new AgentLaunchExtras { Skill = skill });

        Assert.Equal(expected, request.Step!.Policy!.Github);
    }

    [Fact]
    public void A_Step_that_is_not_a_skill_block_asks_for_nothing()
    {
        var address = InstanceAddress.From("T-3/step/1");
        var run = new AgentStepRun(WorkflowInstanceId.Of(address), address, RunToken.From("2.1"), new StepRef("build", "agent"), WriteScope: true);
        var target = new AgentStepTarget(ItemId.From("ITM-" + new string('a', 32)), AgentId.From("claude-code"), "/work/web");

        var request = AgentLaunches.RequestFor(run, target);

        Assert.Null(request.Step!.Policy!.Github);
    }
}
