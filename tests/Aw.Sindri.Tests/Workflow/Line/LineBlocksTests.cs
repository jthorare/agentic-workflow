using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Line;

/// <summary>The design, lens and plan Steps of the line (line spec §4, §5, §6, LN-Y1) as descriptors: what each runs on, reads, reports and writes. Descriptor-only, desk-capable.</summary>
public sealed class LineBlocksTests
{
    private const string Seeds = "fact:scope,fact:packs,fact:design,fact:findings,fact:lensRounds,fact:changedSections,fact:answers,fact:accepted";
    private const string PlanSeeds = "fact:design,fact:plan,fact:findings,fact:questions,fact:answers,fact:answerLog,fact:holdoutSets,fact:anchors";

    private static BlockDescriptor? Find(string id) => FixtureCatalog.Shipped().Find(BlockId.From(id));

    [Theory, Trait("Lane", "unit")]
    //            id, runtime, humanOnly, inputs, outcomes, every, requiredOn "fact=outcome,outcome", capabilities
    [InlineData("step.design.intake", BlockRuntime.Block, false, "fact:scopePacket,fact:designRevision,fact:priorDesignPacket", "fresh,revise", "", "SEEDS", Capability.None)]
    [InlineData("step.design.draft", BlockRuntime.Agent, false, "fact:scope,fact:packs,fact:design,fact:findings,fact:changedSections,fact:designRevision,fact:answers", "drafted", "", "fact:design=drafted", Capability.Tools)]
    [InlineData("step.design.apply", BlockRuntime.Agent, false, "fact:scope,fact:packs,fact:design,fact:designRevision,fact:answers", "applied", "", "fact:design=applied;fact:changedSections=applied", Capability.Tools)]
    [InlineData("step.design.check", BlockRuntime.Block, false, "fact:scope,fact:packs,fact:design", "pass,gaps", "", "fact:findings=gaps", Capability.Tools)]
    [InlineData("step.design.revise", BlockRuntime.Agent, false, "fact:scope,fact:packs,fact:design,fact:findings,fact:answers", "revised", "", "fact:design=revised;fact:changedSections=revised", Capability.Tools)]
    [InlineData("step.lens.ask", BlockRuntime.Desk, true, "fact:escalation", "answered,stop", "", "fact:answers=answered", Capability.None)]
    [InlineData("step.design.lens-desk", BlockRuntime.Desk, true, "fact:findings,fact:lensRounds", "accept,revise,drop", "", "fact:accepted=accept;fact:answers=revise", Capability.None)]
    [InlineData("step.design.evidence", BlockRuntime.Block, false, "fact:scope,fact:packs,fact:design,fact:findings,fact:lensRounds,fact:accepted", "complete,incomplete", "", "fact:designPacket=complete;fact:design=complete;fact:lensRounds=complete;fact:findings=incomplete", Capability.Tools)]
    [InlineData("step.lens.round", BlockRuntime.Block, false, "fact:scope,fact:packs,fact:lensRounds,fact:changedSections", "ready", "", "fact:lensQueue=ready;fact:lensRounds=ready", Capability.Tools)]
    [InlineData("step.lens.run", BlockRuntime.Agent, false, "fact:lensQueue,fact:lensRounds", "reviewed", "", "fact:lensReport=reviewed", Capability.Tools)]
    [InlineData("step.lens.log", BlockRuntime.Block, false, "fact:lensReport,fact:lensRounds,fact:lensQueue", "next,clean,findings,needsOwner", "fact:lensRounds", "fact:lensQueue=next;fact:findings=findings,needsOwner;fact:escalation=needsOwner", Capability.None)]
    [InlineData("step.plan.intake", BlockRuntime.Block, false, "fact:scopePacket,fact:designPacket", "loaded", "", "PLANSEEDS", Capability.None)]
    [InlineData("step.plan.graph", BlockRuntime.Agent, false, "fact:scopePacket,fact:design,fact:plan,fact:findings,fact:answerLog", "drafted", "", "fact:plan=drafted", Capability.Tools)]
    [InlineData("step.plan.check", BlockRuntime.Block, false, "fact:scopePacket,fact:design,fact:plan", "pass,gaps", "", "fact:findings=gaps", Capability.Tools)]
    [InlineData("step.plan.exec-review", BlockRuntime.Agent, false, "fact:design,fact:plan", "executable,objections", "", "fact:findings=objections", Capability.Tools)]
    [InlineData("step.plan.holdout", BlockRuntime.Agent, false, "fact:design,fact:plan", "written", "", "fact:holdoutSets=written", Capability.Tools)]
    [InlineData("step.plan.evidence", BlockRuntime.Block, false, "fact:scopePacket,fact:design,fact:plan,fact:holdoutSets,fact:findings,fact:answerLog", "complete,incomplete", "", "fact:planPacket=complete;fact:findings=incomplete", Capability.Tools)]
    [InlineData("step.plan.questions", BlockRuntime.Block, false, "fact:design,fact:answers,fact:answerLog", "asked,none", "", "fact:questions=asked", Capability.None)]
    [InlineData("step.plan.settle", BlockRuntime.Block, false, "fact:design,fact:answers,fact:answerLog", "assumed,changed,replan", "", "fact:designRevision=changed", Capability.None)]
    [InlineData("step.plan.review", BlockRuntime.Desk, true, "fact:planPacket,fact:design", "approve,reviseDesign,revisePlan,drop", "", "fact:answers=reviseDesign,revisePlan", Capability.None)]
    [InlineData("step.plan.freeze", BlockRuntime.Block, false, "fact:planPacket,fact:design,fact:plan,fact:holdoutSets", "frozen,mismatch", "", "fact:planPacket=frozen;fact:phasePackets=frozen;fact:findings=mismatch", Capability.Tools)]
    public void Each_line_step_declares_what_it_runs_reads_reports_and_writes(
        string id, BlockRuntime runtime, bool humanOnly, string inputs, string outcomes, string every, string required, Capability capabilities)
    {
        var step = Find(id);

        Assert.NotNull(step);
        Assert.Equal(BlockKind.Step, step.Kind);
        Assert.True(step.DeskCapable);
        Assert.Equal(runtime, step.Runtime);
        Assert.Equal(humanOnly ? true : null, step.HumanOnly);
        Assert.Equal(capabilities, step.Capabilities);
        Assert.Equal(Split(inputs, ','), step.Inputs.Select(f => f.Value), StringComparer.Ordinal);
        Assert.Equal(Split(outcomes, ','), step.Outcomes.Select(o => o.Value), StringComparer.Ordinal);
        var written = step.Outputs.Where(o => o.Every == true).Select(o => o.Fact.Value).Concat(step.Outputs.Where(o => o.Every != true).Select(o => o.Fact.Value + "=" + string.Join(',', o.RequiredOn.Select(r => r.Value))));
        var expected = required switch
        {
            "SEEDS" => Seeds.Split(',').Select(f => f + "=fresh,revise"),
            "PLANSEEDS" => PlanSeeds.Split(',').Select(f => f + "=loaded"),
            _ => Split(every, ',').Concat(Split(required, ';')),
        };
        Assert.Equal(expected, written, StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("step.design.draft", "bundle")]
    [InlineData("step.design.apply", "bundle")]
    [InlineData("step.design.revise", "bundle")]
    [InlineData("step.lens.round", "pack")]
    [InlineData("step.lens.run", "subject,bundle")]
    public void The_pack_and_subject_parameters_are_declared_descriptor_only_as_optional_text_but_subject_is_required(string id, string parameters)
    {
        var step = Find(id);

        Assert.NotNull(step);
        Assert.Equal(parameters.Split(','), step.Parameters.Select(p => p.Name), StringComparer.Ordinal);
        Assert.All(step.Parameters, p => Assert.Equal(ParameterType.Text, p.Type));
        Assert.All(step.Parameters, p => Assert.Equal(p.Name == "subject", p.Required));
        Assert.All(step.Parameters, p => Assert.False(p.IsCapabilityField));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_two_stops_are_human_only_and_no_line_step_id_sits_under_the_reserved_prefixes()
    {
        string[] ids = ["step.design.intake", "step.design.lens-desk", "step.lens.run", "step.plan.review", "step.plan.freeze"];

        Assert.All(ids, id => Assert.False(id.StartsWith("step.judge.", StringComparison.Ordinal) || id.StartsWith("step.desk.", StringComparison.Ordinal) || id.StartsWith("input.", StringComparison.Ordinal)));
        Assert.True(Find("step.design.lens-desk")?.HumanOnly);
        Assert.True(Find("step.plan.review")?.HumanOnly);
    }

    private static string[] Split(string text, char separator) => text.Length == 0 ? [] : text.Split(separator);
}
