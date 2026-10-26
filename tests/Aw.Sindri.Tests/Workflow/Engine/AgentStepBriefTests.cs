using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>The brief a launched agent session gets so it knows how to finish its Step (engine P8 F13).</summary>
[Lane(TestLane.Unit)]
public sealed class AgentStepBriefTests
{
    private static readonly InstanceAddress _address = InstanceAddress.From("T-3/scope/1");
    private static readonly AgentStepTarget _target = new(ItemId.From("ITM-" + new string('a', 32)), AgentId.From("claude-code"), "/work/web");

    private static AgentStepRun Run(string block, string state) =>
        new(WorkflowInstanceId.Of(_address), _address, RunToken.From("2.1"), new StepRef(state, block), WriteScope: false);

    private static BlockDescriptor Descriptor(string block) => FixtureCatalog.Shipped().Find(BlockId.From(block))!;   // why: the shipped blocks are the test's subject

    [Fact]
    public void A_one_outcome_step_gets_the_exact_complete_command_with_its_required_outputs()
    {
        var brief = AgentStepBrief.Render(Run("step.scope.draft", "draft"), Descriptor("step.scope.draft"));

        Assert.Equal(
            string.Join('\n',
                "Sindri Step brief. Instance T-3/scope/1, run 2.1, state draft.",
                "You read: fact:brief, fact:graph, fact:coverage, fact:scopeMap, fact:acceptance, fact:findings, fact:roundLog, fact:answerLog, fact:challenge.",
                "Outcome drafted: write fact:scopeMap, fact:acceptance.",
                "Put each output in its own file under the output folder $AW_SINDRI_RUN_DIR/.sindri-out, named after the fact.",
                "Report with: sindri workflow complete T-3/scope/1 --run 2.1 --outcome drafted --output fact:scopeMap=$AW_SINDRI_RUN_DIR/.sindri-out/scopeMap --output fact:acceptance=$AW_SINDRI_RUN_DIR/.sindri-out/acceptance",
                "Your Step ends only when this command succeeds; if it's refused, read the error and fix it."),
            brief);
    }

    [Fact]
    public void A_step_with_several_outcomes_lists_what_each_needs_and_offers_the_choice()
    {
        var brief = AgentStepBrief.Render(Run("step.build.green", "green"), Descriptor("step.build.green"));

        Assert.Contains("Outcome built: write fact:built.", brief, StringComparison.Ordinal);
        Assert.Contains("Outcome blocked: write fact:findings.", brief, StringComparison.Ordinal);
        Assert.Contains("sindri workflow complete T-3/scope/1 --run 2.1 --outcome <built|blocked> --output fact:<key>=$AW_SINDRI_RUN_DIR/.sindri-out/<key>", brief, StringComparison.Ordinal);
        Assert.True(brief.Length < 1500);
    }

    [Fact]
    public void A_step_the_catalog_does_not_know_still_gets_the_command_shape()
    {
        var brief = AgentStepBrief.Render(Run("step.unknown", "x"), descriptor: null);

        Assert.Contains("sindri workflow complete T-3/scope/1 --run 2.1 --outcome <outcome>", brief, StringComparison.Ordinal);
    }

    [Fact]
    public void The_request_carries_the_brief_for_the_run_and_its_descriptor()
    {
        var run = Run("step.scope.draft", "draft");
        var descriptor = Descriptor("step.scope.draft");

        var request = AgentLaunches.RequestFor(run, _target, descriptor);

        Assert.Equal(AgentStepBrief.Render(run, descriptor), request.Step?.Brief);
        Assert.False(string.IsNullOrEmpty(request.Step?.Brief));
    }
}
