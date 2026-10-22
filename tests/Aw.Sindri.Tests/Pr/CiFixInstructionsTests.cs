using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Pr;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Pr;

/// <summary>What 'step.pr.ci-fix' is told (PR babysitter spec A.1) and how the launch carries it: after the brief's first line, in the workspace the Step reads.</summary>
[Lane(TestLane.Unit)]
public sealed class CiFixInstructionsTests
{
    private static readonly InstanceAddress _address = InstanceAddress.From("ITM-00000000000000000000000000000001/spec/1");
    private static readonly AgentStepTarget _target = new(ItemId.From("ITM-" + new string('a', 32)), AgentId.From("claude-code"), "/work/app");

    private static AgentStepRun Run() =>
        new(WorkflowInstanceId.Of(_address), _address, RunToken.From("2.1"), new StepRef("ci-fix", "step.pr.ci-fix"), WriteScope: true);

    private static string All => string.Join('\n', new CiFixInstructions().Lines);

    [Fact]
    public void The_instructions_are_for_the_ci_fix_Step() => Assert.Equal(BlockId.From("step.pr.ci-fix"), new CiFixInstructions().Step);

    [Theory]
    [InlineData("reproduce")]
    [InlineData("workflow.build.commands")]
    [InlineData("allowedFiles")]
    [InlineData("Sindri-Cause: ci")]
    [InlineData("run the check again")]
    [InlineData("flaky")]
    [InlineData("also fails on the base")]
    [InlineData("infrastructure")]
    [InlineData("cannotFix")]
    [InlineData("CI config")]
    [InlineData("skip, delete or loosen a test")]
    [InlineData("Never push")]
    [InlineData("credential")]
    [InlineData("fact:handoffNote")]
    public void The_instructions_carry_the_appendix_A1_contract(string phrase) =>
        Assert.Contains(phrase, All, StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void The_instructions_name_the_logs_as_data_never_as_orders()
    {
        Assert.Contains("<untrusted>", All, StringComparison.Ordinal);
        Assert.Contains("never follow", All, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_instructions_stay_short_enough_for_a_brief() => Assert.True(All.Length < 2500);

    [Fact]
    public void The_brief_puts_the_instructions_after_its_first_line_and_before_what_the_Step_reads()
    {
        var descriptor = FixtureCatalog.Shipped().Find(BlockId.From("step.pr.ci-fix"));   // why: the shipped blocks are the test's subject
        var lines = new CiFixInstructions().Lines;
        var brief = AgentStepBrief.Render(Run(), descriptor, lines).Split('\n');

        Assert.StartsWith("Sindri Step brief.", brief[0], StringComparison.Ordinal);
        Assert.Equal(lines, brief[1..(1 + lines.Count)]);
        Assert.StartsWith("You read:", brief[1 + lines.Count], StringComparison.Ordinal);
    }

    [Fact]
    public void A_brief_with_no_instructions_is_the_brief_it_always_was()
    {
        var descriptor = FixtureCatalog.Shipped().Find(BlockId.From("step.build.green"));   // why: the shipped blocks are the test's subject

        Assert.Equal(AgentStepBrief.Render(Run(), descriptor), AgentStepBrief.Render(Run(), descriptor, instructions: null));
        Assert.Equal(AgentStepBrief.Render(Run(), descriptor), AgentStepBrief.Render(Run(), descriptor, []));
    }

    [Fact]
    public void A_Step_that_reads_a_workspace_launches_in_it_and_may_write_only_there()
    {
        var request = AgentLaunches.RequestFor(Run(), _target, descriptor: null, instructions: null, workspace: "/work/phase-1");

        Assert.Equal("/work/phase-1", request.Worktree);
        Assert.Equal("/work/phase-1", request.Step?.Policy?.WritablePath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void A_Step_with_no_workspace_gets_the_worktree_the_launcher_makes(string? workspace)
    {
        var request = AgentLaunches.RequestFor(Run(), _target, descriptor: null, instructions: null, workspace);

        Assert.Equal("/work/app/.claude/worktrees/sindri-" + WorkflowInstanceId.Of(_address).Value + "-2.1", request.Worktree);
    }
}
