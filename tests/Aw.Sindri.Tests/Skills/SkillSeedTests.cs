using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Testing.Skills;

namespace Aw.Sindri.Tests.Skills;

/// <summary>The seed a skill session gets (skill flows spec D2, §3.1 "args grammar"): the skill's invocation alone, one golden per target and per rule.</summary>
[Lane(TestLane.Unit)]
public sealed class SkillSeedTests
{
    private const string RunDirectory = "/home/u/.agentic-workflow/sindri-app/workflow/runs/WFI-1/fix/LCH-1";

    private static string Seed(string skill, SkillTarget target, SkillBlockManifest block, params (string Key, string Value)[] inputs) =>
        SkillSeed.Render(new SkillLaunch(skill, target, block, inputs.ToDictionary(static i => i.Key, static i => i.Value, StringComparer.Ordinal)), RunDirectory);

    private static void Match(string seed, string name) =>
        Golden.Match(seed + "\n", typeof(SkillSeedTests).Assembly, $"Skills/Golden/{name}.txt", Normalization.None);

    [Fact]
    public void Claude_Code_gets_a_slash_command() => Match(Seed("review", SkillTarget.ClaudeCode, SkillFixtures.Review, ("pr", "42")), "seed-claude-code");

    [Fact]
    public void Codex_gets_a_dollar_skill() => Match(Seed("review", SkillTarget.Codex, SkillFixtures.Review, ("pr", "42")), "seed-codex");

    [Fact]
    public void An_optional_fragment_with_no_value_is_dropped_whole() =>
        Match(Seed("autoplan", SkillTarget.ClaudeCode, SkillFixtures.Autoplan, ("plan", "/home/u/plans/rate-limits")), "seed-fragment-dropped");

    [Fact]
    public void A_fragment_with_a_value_becomes_flag_and_value() =>
        Match(Seed("autoplan", SkillTarget.ClaudeCode, SkillFixtures.Autoplan, ("plan", "/home/u/plans/rate-limits"), ("mode", "growth")), "seed-fragment-kept");

    [Fact]
    public void A_path_with_a_space_is_double_quoted() =>
        Match(Seed("autoplan", SkillTarget.Codex, SkillFixtures.Autoplan, ("plan", "/home/u/my plans/rate-limits")), "seed-path-with-space");

    [Theory]
    [InlineData("a-long-one", "seed-long-text")]
    [InlineData("two\nlines", "seed-multiline-text")]
    [InlineData("/looks like a command", "seed-leading-slash")]
    [InlineData("$(whoami) is just text but a leading dollar is not", "seed-leading-dollar")]
    public void A_text_that_is_long_multiline_or_starts_like_a_command_is_replaced_by_a_pointer_to_its_file(string value, string golden)
    {
        var text = string.Equals(value, "a-long-one", StringComparison.Ordinal) ? new string('x', 501) : value;

        var seed = Seed("officeHours", SkillTarget.ClaudeCode, SkillFixtures.OfficeHours, ("idea", text));

        Assert.DoesNotContain('\n', seed);
        Match(seed, golden);
    }

    [Fact]
    public void A_one_line_text_up_to_500_characters_goes_in_as_is() =>
        Match(Seed("officeHours", SkillTarget.ClaudeCode, SkillFixtures.OfficeHours, ("idea", "a rate limiter for the public API")), "seed-short-text");

    [Fact]
    public void A_skill_launch_gets_the_invocation_as_its_whole_seed_not_the_Step_brief()
    {
        var address = InstanceAddress.From("T-3/step/1");
        var run = new AgentStepRun(WorkflowInstanceId.Of(address), address, RunToken.From("2.1"), new StepRef("fix", "step.skill.review"), WriteScope: true);
        var target = new AgentStepTarget(ItemId.From("ITM-" + new string('a', 32)), AgentId.From("claude-code"), "/work/web");
        var skill = new SkillLaunch("review", SkillTarget.ClaudeCode, SkillFixtures.Review, new Dictionary<string, string>(StringComparer.Ordinal) { ["pr"] = "42" });

        var request = AgentLaunches.RequestFor(run, target, descriptor: null, new AgentLaunchExtras { Skill = skill });

        var brief = request.Step?.Brief;
        Assert.NotNull(brief);
        Assert.StartsWith("/review 42", brief, StringComparison.Ordinal);
        Assert.DoesNotContain("sindri workflow complete", brief, StringComparison.Ordinal);
        Assert.Equal(skill, request.Skill);
    }

    [Theory]
    [InlineData(SkillTarget.ClaudeCode, "seed-trial-claude-code")]
    [InlineData(SkillTarget.Codex, "seed-trial-codex")]
    public void A_trial_reads_the_skill_by_path_for_every_target(SkillTarget target, string golden)
    {
        var launch = new SkillLaunch("review", target, SkillFixtures.Review, new Dictionary<string, string>(StringComparer.Ordinal) { ["pr"] = "42" }) { TrialSkillsDirectory = "/home/u/skills-trial/" };

        Match(SkillSeed.Render(launch, RunDirectory), golden);
    }

    [Fact]
    public void Every_input_is_also_a_file_in_the_run_directory()
    {
        var address = InstanceAddress.From("T-3/step/1");
        var run = new AgentStepRun(WorkflowInstanceId.Of(address), address, RunToken.From("2.1"), new StepRef("fix", "step.skill.review"), WriteScope: true);
        var target = new AgentStepTarget(ItemId.From("ITM-" + new string('a', 32)), AgentId.From("claude-code"), "/work/web");
        var skill = new SkillLaunch("review", SkillTarget.ClaudeCode, SkillFixtures.Review, new Dictionary<string, string>(StringComparer.Ordinal) { ["pr"] = "42" });

        var request = AgentLaunches.RequestFor(run, target, descriptor: null, new AgentLaunchExtras { Skill = skill });

        Assert.Equal("\"42\"", request.Step?.Facts?["pr.json"]);
    }
}
