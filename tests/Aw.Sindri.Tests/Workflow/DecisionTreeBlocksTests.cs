using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>The decision-tree Steps and the evidence provider this build ships as descriptors (DT-R1; plan §3): what each reads, reports and writes.</summary>
public sealed class DecisionTreeBlocksTests
{
    private static BlockDescriptor Block(string id) => FixtureCatalog.Shipped().Find(BlockId.From(id))!;   // why: ShippedBlocksTests pins every shipped id

    [Theory, Trait("Lane", "unit")]
    //            id, runtime, humanOnly, inputs, outcomes, every, requiredOn "fact=outcome,outcome"
    [InlineData("step.intake.ticket", BlockRuntime.Desk, false, "", "done", "", "fact:issue=done;fact:expected=done;fact:actual=done", Capability.None)]
    [InlineData("step.rootcause.investigate", BlockRuntime.Agent, false, "fact:issue,fact:expected,fact:actual", "diagnosed,unfixed", "", "fact:rootCause=diagnosed;fact:findings=unfixed", Capability.Tools)]
    [InlineData("step.reproduce.ui-check", BlockRuntime.Agent, false, "fact:issue,fact:rootCause", "reproduced,notReproduced", "", "fact:check=reproduced;fact:baseline=reproduced;fact:findings=notReproduced", Capability.Tools)]
    [InlineData("step.reproduce.test-check", BlockRuntime.Agent, false, "fact:issue,fact:rootCause", "reproduced,notReproduced", "", "fact:check=reproduced;fact:baseline=reproduced;fact:findings=notReproduced", Capability.Tools)]
    [InlineData("step.bugfix.start-attempt", BlockRuntime.Block, false, "fact:decision", "single,split,capReached", "", "fact:attempt=single,split", Capability.Tools)]
    [InlineData("step.bugfix.record-candidate", BlockRuntime.Block, false, "fact:diff", "recorded", "", "fact:candidate=recorded", Capability.Tools)]
    [InlineData("step.bugfix.recheck", BlockRuntime.Block, false, "fact:check,fact:candidate", "passed,failed", "fact:recheck", "", Capability.Tools)]
    [InlineData("step.bugfix.judge", BlockRuntime.Block, false, "fact:check,fact:candidate,fact:evidenceSummary", "resolved,partial,unresolved,needsHuman", "fact:verdict", "", Capability.Tools)]
    [InlineData("step.report.write", BlockRuntime.Agent, false, "fact:issue", "done", "", "fact:resolution=done", Capability.Tools)]
    [InlineData("step.handtest.run", BlockRuntime.Agent, false, "fact:phasePacket,fact:workspace", "works,broken", "fact:manualTest", "", Capability.Tools)]
    [InlineData("step.verify.journey", BlockRuntime.Agent, false, "fact:phasePacket,fact:workspace", "pass,fail", "fact:evidencePack", "", Capability.Tools)]
    [InlineData("step.verify.design", BlockRuntime.Agent, false, "fact:phasePacket,fact:workspace", "pass,fail,skipped", "", "fact:designCheck=pass,fail", Capability.Tools)]
    [InlineData("step.evidence.ui-plan", BlockRuntime.Agent, false, "fact:phasePacket,fact:built", "planned,noTarget", "", "fact:evidencePlan=planned;fact:evidenceSummary=noTarget", Capability.Tools)]
    [InlineData("step.evidence.api-plan", BlockRuntime.Agent, false, "fact:phasePacket,fact:built", "planned,noTarget", "", "fact:evidencePlan=planned;fact:evidenceSummary=noTarget", Capability.Tools)]
    [InlineData("step.evidence.db-plan", BlockRuntime.Agent, false, "fact:phasePacket,fact:built", "planned,noTarget", "", "fact:evidencePlan=planned;fact:evidenceSummary=noTarget", Capability.Tools)]
    [InlineData("step.evidence.ui-run", BlockRuntime.Agent, false, "fact:evidencePlan,fact:built", "passed,failed,broken", "fact:evidenceSummary", "", Capability.Tools)]
    [InlineData("step.evidence.api-run", BlockRuntime.Agent, false, "fact:evidencePlan,fact:built", "passed,failed", "fact:evidenceSummary", "", Capability.Tools)]
    [InlineData("step.evidence.db-run", BlockRuntime.Agent, false, "fact:evidencePlan,fact:built", "passed,failed", "fact:evidenceSummary", "", Capability.Tools)]
    [InlineData("step.evidence.none", BlockRuntime.Block, false, "fact:built", "done", "", "fact:evidenceSummary=done", Capability.None)]
    [InlineData("step.evidence.combine", BlockRuntime.Block, false, "fact:evidenceUi,fact:evidenceApi,fact:evidenceDb", "done", "", "fact:evidenceSummary=done", Capability.None)]
    [InlineData("step.evidence.desk", BlockRuntime.Desk, true, "fact:built", "recorded,noneFound", "fact:evidenceSummary,fact:evidenceReason", "", Capability.None)]
    [InlineData("step.lens.run", BlockRuntime.Agent, false, "fact:lensQueue,fact:lensRounds", "reviewed", "", "fact:lensReport=reviewed", Capability.Tools)]
    [InlineData("step.review.round", BlockRuntime.Block, false, "fact:change,fact:phasePacket,fact:lensRounds", "ready,noLens", "", "fact:lensQueue=ready;fact:lensRounds=ready", Capability.None)]
    [InlineData("step.review.log", BlockRuntime.Block, false, "fact:lensReport,fact:lensRounds,fact:lensQueue", "next,clean,findings,behaviourWrong,needsOwner", "fact:lensRounds", "fact:lensQueue=next;fact:review=clean,findings,behaviourWrong,needsOwner;fact:escalation=needsOwner", Capability.None)]
    [InlineData("step.review.record", BlockRuntime.Block, false, "fact:change,fact:review,fact:lensRounds,fact:phasePacket", "recorded,recordedWrong", "", "fact:review=recorded,recordedWrong", Capability.Tools)]
    [InlineData("step.review.fix", BlockRuntime.Agent, false, "fact:change,fact:review", "fixed,needsOwner", "", "fact:change=fixed;fact:escalation=needsOwner", Capability.Tools | Capability.WriteScope)]
    [InlineData("step.review.apply-answer", BlockRuntime.Agent, false, "fact:change,fact:review,fact:escalation,fact:answers", "fixed,needsOwner", "", "fact:change=fixed;fact:escalation=needsOwner", Capability.Tools | Capability.WriteScope)]
    [InlineData("step.review.recheck", BlockRuntime.Agent, false, "fact:change,fact:review", "clean,findings,behaviourWrong", "fact:review", "", Capability.Tools)]
    [InlineData("step.owner.decide", BlockRuntime.Desk, true, "", "retry,stop", "", "fact:notes=retry", Capability.None)]
    public void Each_decision_tree_step_declares_what_it_runs_reads_reports_and_writes(
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
        Assert.Equal(Split(every, ',').Concat(Split(required, ';')), written, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_evidence_check_provider_is_registered_descriptor_only()
    {
        var check = Block("evidence.check");

        Assert.Equal(BlockKind.EvidenceProvider, check.Kind);
        Assert.False(check.DeskCapable);
        Assert.Equal(["fact:check"], check.Inputs.Select(f => f.Value), StringComparer.Ordinal);
        Assert.Equal(Capability.Tools, check.Capabilities);
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_human_only_step_is_desk_capable_and_has_no_agent_runtime()
    {
        var humanOnly = FixtureCatalog.Shipped().Blocks.Where(b => b.HumanOnly == true).ToList();

        Assert.Equal(["step.babysit.watch", "step.build.scope-desk", "step.design.lens-desk", "step.desk.choose", "step.evidence.desk", "step.interview.answer", "step.lens.ask", "step.line.decide", "step.owner.decide", "step.phase.decide", "step.plan.review", "step.pr.docs-decide", "step.pr.docs-merge-decide", "step.pr.owner-comment", "step.pr.rework-notes", "step.scope.review", "step.ship.open"], humanOnly.Select(b => b.Id.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.All(humanOnly, b => Assert.True(b.DeskCapable));
        Assert.All(humanOnly, b => Assert.NotEqual(BlockRuntime.Agent, b.Runtime));
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_new_outcome_and_key_is_camelCase()
    {
        var steps = FixtureCatalog.Shipped().Blocks.Where(b => b.Runtime is not null && b.Id.Value != "step.judge.choose").ToList();

        Assert.NotEmpty(steps);
        foreach (var step in steps)
        {
            Assert.All(step.Outcomes, o => Assert.Matches("^[a-z][A-Za-z0-9]*$", o.Value));
            Assert.All(step.Inputs.Concat(step.Outputs.Select(o => o.Fact)), f => Assert.Matches("^fact:[a-z][A-Za-z0-9]*$", f.Value));
        }
    }

    private static string[] Split(string text, char separator) => text.Length == 0 ? [] : text.Split(separator);
}
