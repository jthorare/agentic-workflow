using System.Text.Json;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Evals;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// 'recipe eval --record' writes each answer with the variant that fitted what it answered (spec §6.3, §9.2): a text
/// answer on an answer wait or a Step's needs-answer park, '--resume' (with any text) on a needs-answer (wait) park,
/// '--accept' or '--reject' on an escalated gate. Every case here is recorded from a replayed history and replays
/// again; the answer wait's transition is gated, so the eval runs its gates from the answer line (spec §3.4, §5.8).
/// </summary>
public sealed class EvalRecorderAnswerTests
{
    private const string Start = """{"start":{"inputs":{}}}""";
    private const string Done = """{"state":"done","outcome":"resolved"}""";
    private const string WaitPark = """{"state":"parked","park":"needs-answer"}""";

    [Fact, Trait("Lane", "unit")]
    public void A_text_answer_on_an_answer_wait_whose_gate_refuses_is_recorded_as_text()
    {
        var recorded = RoundTrip(Gated("gate.test.check"),
            Line("""{"kind":"answer","variant":"text","outputs":{"fact:answer":"Only the CLI."}}""", """{"gate.test.check":"fail"}""", WaitPark),
            Line("""{"kind":"answer","variant":"resume"}""", gates: null, Done));

        Assert.Equal([EvalVariant.Text, EvalVariant.Resume], recorded.Steps.Select(s => s.Trigger.Variant));
        Assert.Equal("Only the CLI.", Text(recorded.Steps[0]));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_text_answer_on_an_answer_wait_that_escalates_is_recorded_as_text_and_the_accept_as_accept()
    {
        var recorded = RoundTrip(Gated("gate.test.model"),
            Line("""{"kind":"answer","variant":"text","outputs":{"fact:answer":"Only the CLI."}}""", """{"gate.test.model":"uncertain"}""", """{"state":"escalated"}"""),
            Line("""{"kind":"answer","variant":"accept"}""", gates: null, Done));

        Assert.Equal([EvalVariant.Text, EvalVariant.Accept], recorded.Steps.Select(s => s.Trigger.Variant));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_reject_on_an_escalated_answer_wait_is_recorded_as_reject()
    {
        var recorded = RoundTrip(Gated("gate.test.model"),
            Line("""{"kind":"answer","variant":"text","outputs":{"fact:answer":"Only the CLI."}}""", """{"gate.test.model":"uncertain"}""", """{"state":"escalated"}"""),
            Line("""{"kind":"answer","variant":"reject"}""", gates: null, WaitPark));

        Assert.Equal([EvalVariant.Text, EvalVariant.Reject], recorded.Steps.Select(s => s.Trigger.Variant));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_resume_on_a_needs_answer_wait_park_that_is_refused_again_is_recorded_as_resume()
    {
        var recorded = RoundTrip(Gated("gate.test.check"),
            Line("""{"kind":"answer","variant":"text","outputs":{"fact:answer":"Only the CLI."}}""", """{"gate.test.check":"fail"}""", WaitPark),
            Line("""{"kind":"answer","variant":"resume"}""", """{"gate.test.check":"fail"}""", WaitPark),
            Line("""{"kind":"answer","variant":"resume"}""", gates: null, Done));

        Assert.Equal([EvalVariant.Text, EvalVariant.Resume, EvalVariant.Resume], recorded.Steps.Select(s => s.Trigger.Variant));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_resume_with_text_on_a_needs_answer_wait_park_is_recorded_as_resume_with_its_text()
    {
        var recorded = RoundTrip(Gated("gate.test.check"),
            Line("""{"kind":"answer","variant":"text","outputs":{"fact:answer":"Only the CLI."}}""", """{"gate.test.check":"fail"}""", WaitPark),
            Line("""{"kind":"answer","variant":"resume","outputs":{"fact:answer":"The CLI and the docs."}}""", gates: null, Done));

        Assert.Equal([EvalVariant.Text, EvalVariant.Resume], recorded.Steps.Select(s => s.Trigger.Variant));
        Assert.Equal("The CLI and the docs.", Text(recorded.Steps[1]));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_text_answer_repeating_the_last_answer_is_recorded_as_text_with_that_text()
    {
        var recorded = RoundTrip(TwoQuestions(),
            Line("""{"kind":"answer","variant":"text","outputs":{"fact:answer":"Yes."}}""", gates: null, """{"state":"again"}"""),
            Line("""{"kind":"answer","variant":"text","outputs":{"fact:answer":"Yes."}}""", gates: null, Done));

        Assert.Equal([EvalVariant.Text, EvalVariant.Text], recorded.Steps.Select(s => s.Trigger.Variant));
        Assert.Equal("Yes.", Text(recorded.Steps[1]));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("accept", EvalVariant.Accept, """{"state":"done","outcome":"resolved"}""")]
    [InlineData("reject", EvalVariant.Reject, """{"state":"parked","park":"needs-answer"}""")]
    public void An_answer_on_a_step_gate_escalation_is_recorded_as_the_option_it_took(string option, EvalVariant variant, string expect)
    {
        var recorded = RoundTripReportingSteps(StepGated(),
            Line("""{"kind":"step.completed","outcome":"done","outputs":{"fact:output":"x"}}""", """{"gate.test.model":"uncertain"}""", """{"state":"escalated"}"""),
            Line("{\"kind\":\"answer\",\"variant\":\"" + option + "\"}", gates: null, expect));

        Assert.Equal(variant, recorded.Steps[1].Trigger.Variant);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(AnswerVariant.Accept)]
    [InlineData(AnswerVariant.Reject)]
    public void An_answer_on_a_gate_escalation_whose_pending_budget_park_takes_effect_is_recorded_as_an_option_that_fits(AnswerVariant given)
    {
        // A pending park in the pure engine comes only from a drift or a takeover, so eval lines can't reach this row:
        // the escalation is decided from a case, the budget park set pending by hand (as AnswerRoundCapTests does), and
        // the engine decides the answer. Both options end the escalation the same way, so either replays; resume doesn't fit.
        var recipe = StepGated();
        var escalating = Case(
            Line("""{"kind":"step.completed","outcome":"done","outputs":{"fact:output":"x"}}""", """{"gate.test.model":"uncertain"}""", """{"state":"escalated"}"""));
        var (verdict, history) = EvalRunner.Replay(escalating, recipe, EngineFixture.Catalog, InMemoryChildRecipes.None);
        Assert.Null(verdict.Detail);
        var escalated = history[^1].After with { PendingPark = AnswerRoundCapTests.BudgetPark };
        var answer = new AnswerGiven("human:cli", new CommandStamp(CommandNonce.From(new string('c', 32)), new string('d', 64)), given, Text: null, Amount: null, Forwarded: null);
        var answered = Assert.IsType<Applied>(TransitionDecision.Decide(new DecisionInput(escalated, recipe, answer, GateAnswers.None, EngineFixture.Context()))).Changes[0];
        Assert.Equal((EngineStates.Parked, ParkReason.Budget), (answered.After.State, answered.After.Park));

        var recorded = EvalRecorder.Record(
            "recorded", [.. history.SkipLast(1), history[^1] with { After = escalated }, answered],
            new Dictionary<TriggerKey, IReadOnlyDictionary<FactKey, FactValue>>(), Contents(escalating)).Value;

        var line = recorded.Steps[^1];
        Assert.Equal(EvalVariant.Accept, line.Trigger.Variant);
        var replayed = new EvalSimulation(recipe, EngineFixture.Catalog, InMemoryChildRecipes.None, escalated, []).Step(line, 3);
        Assert.True(replayed.IsSuccess, replayed.IsError ? replayed.Error.Message : null);
        Assert.Null(replayed.Value.Next.Diverges(line.Expect));
    }

    [Fact, Trait("Lane", "unit")]
    public void Two_identical_text_answers_on_a_refused_step_are_both_recorded_as_text_with_their_text()
    {
        // A refused report parks the Step with needs-answer; a failed Step no transition takes now retries and parks as unavailable, which takes no text answer.
        var refused = """{"kind":"step.completed","outcome":"done","outputs":{"fact:output":"x"}}""";
        var gate = """{"gate.test.model":"fail"}""";
        var parked = """{"state":"parked","park":"needs-answer"}""";
        var recorded = RoundTripReportingSteps(StepGated(),
            Line(refused, gate, parked),
            Line("""{"kind":"answer","variant":"text","outputs":{"fact:answer":"Yes."}}""", gates: null, """{"state":"produce"}"""),
            Line(refused, gate, parked),
            Line("""{"kind":"answer","variant":"text","outputs":{"fact:answer":"Yes."}}""", gates: null, """{"state":"produce"}"""));

        Assert.Equal([EvalVariant.Text, EvalVariant.Text], recorded.Steps.Where(s => s.Trigger.Kind == "answer").Select(s => s.Trigger.Variant));
        Assert.Equal("Yes.", Text(recorded.Steps[3]));
    }

    /// <summary>The original case replays; its history, recorded with only the events' deltas (no report saved for any trigger), replays too.</summary>
    private static EvalCase RoundTrip(Recipe recipe, params string[] lines) => RoundTrip(recipe, reportSteps: false, lines);

    /// <summary>
    /// <see cref="RoundTrip(Recipe, string[])"/> with the Step reports saved as the interpreter saves them: a refused
    /// report's outputs aren't in its event's delta, so only the saved report carries them (spec §9.2). Answers still
    /// record from the deltas.
    /// </summary>
    private static EvalCase RoundTripReportingSteps(Recipe recipe, params string[] lines) => RoundTrip(recipe, reportSteps: true, lines);

    private static EvalCase RoundTrip(Recipe recipe, bool reportSteps, string[] lines)
    {
        var original = Case(lines);
        var (verdict, history) = EvalRunner.Replay(original, recipe, EngineFixture.Catalog, InMemoryChildRecipes.None);
        Assert.Null(verdict.Detail);
        var reported = reportSteps
            ? history.Skip(1).Zip(original.Steps).Where(static p => p.Second.Trigger.Kind.StartsWith("step.", StringComparison.Ordinal)).ToDictionary(
                static p => p.First.Event.Trigger.Key,
                static p => (IReadOnlyDictionary<FactKey, FactValue>)(p.Second.Trigger.Outputs ?? new Dictionary<FactKey, JsonElement>())
                    .ToDictionary(static o => o.Key, static o => EvalFacts.Value(o.Value)))
            : [];

        var recorded = EvalRecorder.Record("recorded", history, reported, Contents(original)).Value;

        Assert.Null(EvalRunner.Run(recorded, recipe, EngineFixture.Catalog, InMemoryChildRecipes.None).Detail);
        Assert.Equal(original.Steps.Select(s => s.Expect.State), recorded.Steps.Select(s => s.Expect.State), StringComparer.Ordinal);
        return recorded;
    }

    private static EvalCase Case(params string[] lines) =>
        Assert.IsType<EvalCase>(EvalCaseReader.Read("case", string.Join('\n', [Start, .. lines]), EvalComposition.Options).Case);

    private static string Line(string trigger, string? gates, string expect) =>
        "{\"trigger\":" + trigger + (gates is null ? string.Empty : ",\"gates\":" + gates) + ",\"expect\":" + expect + "}";

    private static string? Text(EvalStepLine line) =>
        line.Trigger.Outputs?.GetValueOrDefault(FactKey.From("fact:answer")).GetString();

    /// <summary>'produce' (a desk Step) goes to 'done' through the top-rung 'gate.test.model'; a step.failed has no transition, so it parks needs-answer.</summary>
    private static Recipe StepGated() => EngineFixture.Minimal([EngineFixture.From("done", when: null, "gate.test.model")]);

    /// <summary>The answer-wait recipe with <paramref name="gate"/> on 'asking -> done'.</summary>
    private static Recipe Gated(string gate)
    {
        var ask = Ask();
        var use = new GateUse(BlockId.From(gate), Threshold: gate == "gate.test.model" ? 0.8 : null, new Dictionary<string, WithValue>(StringComparer.Ordinal));
        return ask with { Transitions = [ask.Transitions[0] with { Gates = [use] }] };
    }

    /// <summary>Two answer waits in a row: 'asking' then 'again', then done.</summary>
    private static Recipe TwoQuestions()
    {
        var ask = Ask();
        var asking = ask.States["asking"];
        var again = asking with { WaitFor = new WaitFor(Event: null, new AnswerWait(AnswerId.From("again"), "Still sure?"), Resource: null, Child: null) };
        var states = new Dictionary<string, RecipeState>(ask.States, StringComparer.Ordinal) { ["again"] = again };
        return ask with
        {
            States = states,
            Transitions = [ask.Transitions[0] with { To = "again" }, ask.Transitions[0] with { From = ["again"] }],
        };
    }

    private static Recipe Ask()
    {
        var document = RecipeComposition.ReadClean(AnswerWaitTests.Yaml, "workflow.ask.yaml");
        return document.Recipe with { Hash = document.Hash };
    }

    private static Dictionary<string, JsonElement> Contents(EvalCase evalCase)
    {
        var contents = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var content in evalCase.Steps.SelectMany(s => s.Trigger.Outputs?.Values ?? []))
        {
            contents[EvalFacts.Value(content).Ref] = content;
        }

        return contents;
    }
}
