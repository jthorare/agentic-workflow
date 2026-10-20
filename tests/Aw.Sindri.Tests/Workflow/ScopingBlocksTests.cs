using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The scoping station's Steps and the context Step inputs it names, as descriptors (scoping spec §9, SC-Y): what each runs on, reads, reports and writes.</summary>
public sealed class ScopingBlocksTests
{
    private const string Seeds = "fact:scopeMap,fact:acceptance,fact:findings,fact:answers,fact:answerLog,fact:challenge,fact:questions,fact:openQuestions";

    private static BlockDescriptor Block(string id) => FixtureCatalog.Shipped().Find(BlockId.From(id))!;   // why: ShippedBlocksTests pins every shipped id

    [Theory, Trait("Lane", "unit")]
    //            id, runtime, humanOnly, inputs, outcomes, every, requiredOn "fact=outcome,outcome", capabilities
    [InlineData("step.scope.intake", BlockRuntime.Block, false, "fact:scopeInput", "brief,tracker,idea,change", "", "SEEDS", Capability.None)]
    [InlineData("step.scope.read-brief", BlockRuntime.Block, false, "fact:scopeInput", "read,empty", "", "fact:brief=read;fact:anchors=read", Capability.Tools)]
    [InlineData("step.scope.read-tracker", BlockRuntime.Block, false, "fact:scopeInput", "read,empty", "", "fact:brief=read;fact:anchors=read", Capability.Tools | Capability.Network)]
    [InlineData("step.scope.read-change", BlockRuntime.Block, false, "fact:scopeInput", "read,empty", "", "fact:brief=read;fact:anchors=read", Capability.Tools)]
    [InlineData("step.brief.seed", BlockRuntime.Block, false, "", "seeded", "", "fact:answers=seeded;fact:answerLog=seeded", Capability.None)]
    [InlineData("step.brief.questions", BlockRuntime.Agent, false, "fact:idea,fact:answers,fact:answerLog", "asked,ready", "", "fact:questions=asked;fact:interviewClosed=ready", Capability.Tools)]
    [InlineData("step.brief.write", BlockRuntime.Agent, false, "fact:idea,fact:answers,fact:answerLog", "written,empty", "", "fact:brief=written", Capability.Tools)]
    [InlineData("step.scope.anchor", BlockRuntime.Block, false, "fact:brief", "anchored", "", "fact:anchors=anchored", Capability.Tools)]
    [InlineData("step.scope.gather", BlockRuntime.Block, false, "fact:anchors", "gathered", "", "fact:lenses=gathered;fact:coverage=gathered", Capability.None)]
    [InlineData("step.scope.draft", BlockRuntime.Agent, false, "fact:brief,fact:lenses,fact:coverage,fact:scopeMap,fact:acceptance,fact:findings,fact:answerLog,fact:challenge", "drafted", "", "fact:scopeMap=drafted;fact:acceptance=drafted", Capability.Tools)]
    [InlineData("step.scope.check", BlockRuntime.Block, false, "fact:brief,fact:lenses,fact:coverage,fact:scopeMap,fact:acceptance,fact:answers,fact:answerLog", "pass,gaps,regather", "", "fact:findings=gaps,regather", Capability.Tools)]
    [InlineData("step.interview.questions", BlockRuntime.Agent, false, "fact:scopeMap,fact:acceptance,fact:lenses,fact:answerLog,fact:challenge,fact:openQuestions", "asked,none", "", "fact:questions=asked;fact:openQuestions=asked,none", Capability.Tools)]
    [InlineData("step.interview.answer", BlockRuntime.Desk, true, "fact:questions,fact:answerLog", "answered,doneAsking", "", "fact:answers=answered,doneAsking;fact:interviewClosed=doneAsking", Capability.None)]
    [InlineData("step.interview.record", BlockRuntime.Block, false, "fact:answers,fact:answerLog", "recorded", "", "fact:answerLog=recorded", Capability.None)]
    [InlineData("step.interview.apply", BlockRuntime.Agent, false, "fact:brief,fact:lenses,fact:coverage,fact:questions,fact:answers,fact:answerLog,fact:scopeMap,fact:acceptance,fact:openQuestions,fact:anchors", "updated,rescope", "", "fact:scopeMap=updated,rescope;fact:acceptance=updated,rescope;fact:openQuestions=updated,rescope", Capability.Tools)]
    [InlineData("step.scope.reanchor", BlockRuntime.Block, false, "fact:scopeMap,fact:anchors,fact:lenses", "anchored", "", "fact:anchors=anchored", Capability.Tools)]
    [InlineData("step.scope.challenge", BlockRuntime.Agent, false, "fact:brief,fact:lenses,fact:coverage,fact:scopeMap,fact:acceptance,fact:answerLog,fact:challenge", "nothingNew,found", "fact:challengeRound", "", Capability.Tools)]
    [InlineData("step.scope.challenge-log", BlockRuntime.Block, false, "fact:challenge,fact:challengeRound", "nothingNew,found", "fact:challenge", "fact:findings=found", Capability.None)]
    [InlineData("step.scope.propose", BlockRuntime.Block, false, "fact:scopeMap", "proposed,skipped", "fact:scopeMap", "", Capability.Tools)]
    [InlineData("step.scope.evidence", BlockRuntime.Block, false, "fact:brief,fact:lenses,fact:coverage,fact:scopeMap,fact:acceptance,fact:answers,fact:answerLog,fact:challenge,fact:openQuestions,fact:decision,fact:anchors,fact:questions", "complete,incomplete,regather", "", "fact:scopeMap=complete;fact:acceptance=complete;fact:coverage=complete;fact:openQuestions=complete;fact:scopePacket=complete;fact:findings=incomplete,regather", Capability.Tools)]
    [InlineData("step.scope.review", BlockRuntime.Desk, true, "fact:scopePacket", "approve,revise,drop", "", "fact:answers=revise", Capability.None)]
    [InlineData("step.scope.freeze", BlockRuntime.Block, false, "fact:scopePacket,fact:brief,fact:lenses,fact:coverage,fact:scopeMap,fact:acceptance,fact:answers,fact:answerLog,fact:challenge,fact:openQuestions,fact:decision,fact:anchors,fact:questions", "frozen,mismatch", "", "fact:scopePacket=frozen;fact:findings=mismatch", Capability.Tools)]
    [InlineData("step.scope.withdraw", BlockRuntime.Block, false, "", "withdrawn", "", "", Capability.Tools)]
    public void Each_scoping_step_declares_what_it_runs_reads_reports_and_writes(
        string id, BlockRuntime runtime, bool humanOnly, string inputs, string outcomes, string every, string required, Capability capabilities)
    {
        var step = Block(id);

        Assert.Equal(BlockKind.Step, step.Kind);
        Assert.True(step.DeskCapable);
        Assert.Equal(runtime, step.Runtime);
        Assert.Equal(humanOnly ? true : null, step.HumanOnly);
        Assert.Equal(capabilities, step.Capabilities);
        Assert.Equal(Split(inputs, ','), step.Inputs.Select(f => f.Value), StringComparer.Ordinal);
        Assert.Equal(Split(outcomes, ','), step.Outcomes.Select(o => o.Value), StringComparer.Ordinal);
        var written = step.Outputs.Where(o => o.Every == true).Select(o => o.Fact.Value).Concat(step.Outputs.Where(o => o.Every != true).Select(o => o.Fact.Value + "=" + string.Join(',', o.RequiredOn.Select(r => r.Value))));
        var expected = required == "SEEDS" ? Seeds.Split(',').Select(f => f + "=brief,tracker,idea,change") : Split(every, ',').Concat(Split(required, ';'));
        Assert.Equal(expected, written, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_four_readers_and_the_idea_child_hand_back_exactly_what_intake_seeds()
    {
        var intake = Block("step.scope.intake");

        Assert.Equal(Seeds.Split(','), intake.Outputs.Select(o => o.Fact.Value), StringComparer.Ordinal);   // §6.1: all eight, RequiredOn each of the four outcomes
        Assert.All(intake.Outputs, o => Assert.Equal(intake.Outcomes.Select(x => x.Value), o.RequiredOn.Select(x => x.Value), StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_scoping_agent_step_declares_tools_and_neither_effects_nor_write_scope_nor_network()
    {
        string[] ids = ["step.brief.questions", "step.brief.write", "step.scope.draft", "step.interview.questions", "step.interview.apply", "step.scope.challenge"];
        var agents = ids.Select(Block).ToList();

        Assert.All(agents, a => Assert.Equal(Capability.Tools, a.Capabilities));   // the read-only capability is W9/L-17's and has no descriptor field yet; this only pins what the descriptor can say
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_intake_kind_has_a_happy_path_eval()
    {
        // idea is the one kind whose path runs through the child, so its eval is named for that (scoping spec Appendix B.1: idea-child)
        var expected = Block("step.scope.intake").Outcomes.Select(o => o.Value == "idea" ? "idea-child" : $"{o.Value}-happy").Order(StringComparer.Ordinal);
        var shipped = ShippedRecipes.Evals.Where(e => e.StartsWith("sindri/evals/workflow.scoping/", StringComparison.Ordinal)).Select(e => Path.GetFileNameWithoutExtension(e));

        Assert.Empty(expected.Except(shipped, StringComparer.Ordinal));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("input.context.lenses", "anchors,depth,hops")]
    [InlineData("input.context.code", "anchors")]
    [InlineData("input.context.history", "anchors,window")]
    public void The_context_inputs_are_registered_descriptor_only_with_the_parameters_the_recipe_passes(string id, string parameters)
    {
        var input = Block(id);

        Assert.Equal(BlockKind.StepInput, input.Kind);
        Assert.False(input.DeskCapable);
        Assert.Equal(parameters.Split(','), input.Parameters.Select(p => p.Name), StringComparer.Ordinal);
    }

    private static string[] Split(string text, char separator) => text.Length == 0 ? [] : text.Split(separator);
}
