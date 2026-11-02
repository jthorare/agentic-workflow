using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// Repairing a stuck scoping run in place (#1270, asks 1 and 2): 'workflow rewind' re-enters an earlier state and keeps the facts, and
/// 'workflow answer --record' hands the interview desk a batch that supersedes earlier answers. The owner's case is P-FRN-920 line/5:
/// parked no-progress with two answers recorded as ids that do not exist.
/// </summary>
public sealed class RewindTests
{
    private static readonly string[] _walked =
    [
        "intake", "read-brief", "gather", "relevance", "relevance-judge", "relevance-apply", "draft", "check", "questions", "interview", "record", "apply",
        "challenge", "challenge-log", "size", "propose", "evidence", "stop", "freeze", "withdraw",
    ];

    private static Recipe Scoping => RecipeComposition.ReadClean(ShippedRecipes.Read("sindri/workflow.scoping.yaml"), "workflow.scoping.yaml").Recipe;

    private static CommandStamp Stamp(char c) => new(CommandNonce.From(string.Concat(Enumerable.Repeat(((byte)c).ToString("x2", System.Globalization.CultureInfo.InvariantCulture), 16))), new string('0', 64));

    private static RewindRequested Rewind(string to) => new("human:cli", Stamp('r'), to);

    private static readonly string[] _factNames = ["fact:scopeMap", "fact:acceptance", "fact:questions", "fact:answerLog", "fact:answers", "fact:openQuestions", "fact:findings", "fact:challenge"];

    /// <summary>The owner's instance: past every state once, parked no-progress after evidence's findings came back for the third time.</summary>
    private static WorkflowInstance Parked() => At(Scoping, "draft", entry: 31) with
    {
        Visited = _walked,
        Facts = _factNames.ToDictionary(static n => FactKey.From(n), static n => Fact(n), EqualityComparer<FactKey>.Default),
        Suspension = new Suspension(
            SuspensionKind.Park, ParkReason.NoProgress, ParkKind.Loop, Escalation: null, "rounds made no progress", "service:engine", Mirrors: null,
            new ResumeAction(ResumeKind.Rerun, Withheld: null, Gate: null, Effects: [])),
    };

    private static Decision Decide(WorkflowInstance instance, Trigger trigger) =>
        TransitionDecision.Decide(new DecisionInput(instance, Scoping, trigger, GateAnswers.None, EngineFixture.Context()));

    private static WorkflowInstance After(Decision decision) => Assert.IsType<Applied>(decision).Changes[0].After;

    /// <summary>The pre-#1241 log: A1 and A2 name ids ('Q91', 'Q96') that the facts do not hold, which is all SCP-ACC-003 finds.</summary>
    private static ScopingFacts OwnersFacts() => ScopingFacts.Expiry().Without("rounds").Without("scopePacket")
        .Edit("answerLog", n =>
        {
            n[0]!["recordedAs"] = new JsonArray("Q91");
            n[1]!["recordedAs"] = new JsonArray("Q96");
        });

    private static string[] Acc003(ScopingFacts facts) =>
        [.. ScopePacketChecks.Run(new ScopeFacts(facts), ScopeStage.Evidence).Findings.Where(static f => f.Rule == "SCP-ACC-003").Select(static f => f.Item ?? "?")];

    private const string Batch = "[{\"batch\":3,\"round\":3,\"actor\":\"owner\",\"at\":\"2026-10-26T09:00:00Z\",\"questionIds\":[\"Q1\",\"Q2\"],\"recordedAs\":[\"AC-2\",\"AC-3\"],\"supersedes\":[\"A1\",\"A2\"],"
        + "\"answers\":[{\"question\":\"Q1\",\"option\":\"Q1.a\",\"text\":\"Add a choice; links with no time never expire\"},{\"question\":\"Q2\",\"option\":\"Q2.a\",\"text\":\"They never expire\"}]}]";

    [Fact, Trait("Lane", "unit")]
    public async Task A_parked_scoping_run_rewinds_to_interview_a_batch_superseding_A1_and_A2_is_recorded_and_evidence_passes_SCP_ACC_003()
    {
        var facts = OwnersFacts();
        Assert.Equal(["A1", "A2"], Acc003(facts));

        var parked = Parked();
        var back = After(Decide(parked, Rewind("interview")));
        Assert.Equal(("interview", 32, null), (back.Frame.State, back.Frame.Entry, back.Suspension));
        Assert.Equal(StepRuntime.Desk, back.Frame.Run?.Runtime);
        Assert.Equal(parked.Facts, back.Facts);
        Assert.Equal(RunToken.For(32, 1), AnswerRecording.InterviewRun(back, Scoping).Value);

        facts["answers"] = Batch;
        var recorded = (await new ScopingDeskService().Handle(new ScopingDeskRequest("step.interview.record", facts), CancellationToken.None)).Value;
        Assert.Equal("recorded", recorded.Outcome);
        facts["answerLog"] = recorded.Outputs["answerLog"];

        Assert.Equal(["A1", "A2", "A3"], [.. JsonNode.Parse(facts["answerLog"])!.AsArray().Select(static e => e!["id"]!.GetValue<string>())]);
        Assert.Empty(Acc003(facts));
    }

    [Fact, Trait("Lane", "unit")]
    public void Rewind_refuses_while_the_open_run_has_a_live_agent_and_names_its_launch()
    {
        var launch = "LCH-" + new string('a', 32);
        var live = At(Scoping, "draft", entry: 5) with { Visited = _walked };
        live = live with { Frame = live.Frame with { Run = new OpenRun(RunToken.For(5, 1), StepRuntime.Agent, launch, Task: null) } };

        var rejected = Assert.IsType<Rejected>(Decide(live, Rewind("interview")));

        Assert.Equal("SND-WF-003", rejected.Error.Code);
        Assert.Contains(launch, rejected.Error.Message, StringComparison.Ordinal);
        Assert.Contains("end or wait", rejected.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("propose")]
    [InlineData("freeze")]
    [InlineData("withdraw")]
    [InlineData("stop")]
    [InlineData("scoped")]
    public void Rewind_to_a_state_with_effects_or_an_end_refuses_and_names_it(string target)
    {
        var rejected = Assert.IsType<Rejected>(Decide(Parked(), Rewind(target)));

        Assert.Equal("SND-WF-003", rejected.Error.Code);
        Assert.Contains(target, rejected.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Answer_record_refuses_outside_interview_and_says_to_rewind_to_interview_first()
    {
        var refused = AnswerRecording.InterviewRun(Parked(), Scoping);

        Assert.True(refused.IsError);
        Assert.Equal("SND-WF-003", refused.Error.Code);
        Assert.Contains("rewind to interview first", refused.Error.Message, StringComparison.Ordinal);
    }
}
