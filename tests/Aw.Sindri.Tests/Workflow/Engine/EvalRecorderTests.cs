using System.Text.Json;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Evals;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>'recipe eval --record' (spec §6.3, §9.2): an instance's events, with the store's fact content, written back as a case that replays.</summary>
public sealed class EvalRecorderTests
{
    private static readonly Core.Workflow.Recipes.Recipe _step = EvalComposition.Recipe("sindri/workflow.step.yaml");
    private static readonly Core.Workflow.Blocks.BlockCatalog _catalog = EvalComposition.Catalog;

    private static readonly string[] _lines =
    [
        """{"start":{"inputs":{"fact:spec":"Add a --version flag."}}}""",
        """{"trigger":{"kind":"step.completed","outcome":"done","outputs":{"fact:output":"Added --version."}},"expect":{"state":"checks"}}""",
        """{"trigger":{"kind":"step.completed","outcome":"failed","outputs":{"fact:findings":"printed nothing"}},"expect":{"state":"produce"}}""",
        """{"trigger":{"kind":"step.completed","outcome":"done","outputs":{"fact:output":"Wired it."}},"expect":{"state":"checks"}}""",
        """{"trigger":{"kind":"step.completed","outcome":"failed","outputs":{"fact:findings":"printed nothing"}},"expect":{"state":"parked","park":"no-progress"}}""",
        """{"trigger":{"kind":"answer","variant":"grant","amount":2},"expect":{"state":"produce"}}""",
        """{"trigger":{"kind":"takeover"},"expect":{"state":"taken-over"}}""",
        """{"trigger":{"kind":"handback","variant":"handback"},"expect":{"state":"produce"}}""",
        """{"trigger":{"kind":"step.completed","outcome":"done","outputs":{"fact:output":"Read it from the assembly."}},"expect":{"state":"checks"}}""",
        """{"trigger":{"kind":"step.completed","outcome":"passed"},"expect":{"state":"verify"}}""",
        """{"trigger":{"kind":"step.completed","outcome":"accepted"},"expect":{"state":"done","outcome":"resolved"}}""",
    ];

    [Fact, Trait("Lane", "unit")]
    public void A_recorded_history_replays_as_the_case_it_came_from()
    {
        var original = Case();
        var (verdict, history) = EvalRunner.Replay(original, _step, _catalog, InMemoryChildRecipes.None);
        Assert.Null(verdict.Detail);

        var recorded = EvalRecorder.Record("recorded", history, Reported(original, history), Contents(original)).Value;

        Assert.Null(EvalRunner.Run(recorded, _step, _catalog, InMemoryChildRecipes.None).Detail);
        Assert.Equal(original.Steps.Select(s => s.Trigger.Kind), recorded.Steps.Select(s => s.Trigger.Kind), StringComparer.Ordinal);
        Assert.Equal(original.Steps.Select(s => s.Expect.State), recorded.Steps.Select(s => s.Expect.State), StringComparer.Ordinal);
        var grant = recorded.Steps[4].Trigger;
        Assert.Equal((EvalVariant.Grant, 2), (grant.Variant, grant.Amount));
        Assert.Equal(EvalVariant.Takeover, recorded.Steps[5].Trigger.Variant);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_history_that_does_not_start_with_workflow_started_is_refused()
    {
        var (_, history) = EvalRunner.Replay(Case(), _step, _catalog, InMemoryChildRecipes.None);

        var result = EvalRecorder.Record("recorded", [.. history.Skip(1)], Reported(Case(), history), Contents(Case()));

        Assert.Contains("doesn't start with workflow.started", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_history_with_another_instances_event_is_refused()
    {
        var (_, history) = EvalRunner.Replay(Case(), _step, _catalog, InMemoryChildRecipes.None);
        var other = WorkflowInstanceId.From("WFI-" + new string('9', 32));
        var stray = history[2] with { After = history[2].After with { Id = other }, Event = history[2].Event with { Instance = other } };

        var result = EvalRecorder.Record("recorded", [history[0], history[1], stray], Reported(Case(), history), Contents(Case()));

        Assert.Contains("event 3 of the history is for " + other.Value, result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Fact_content_comes_from_the_stored_facts_and_a_missing_one_is_an_error()
    {
        var (_, history) = EvalRunner.Replay(Case(), _step, _catalog, InMemoryChildRecipes.None);

        var result = EvalRecorder.Record("recorded", history, Reported(Case(), history), new Dictionary<string, JsonElement>(StringComparer.Ordinal));

        Assert.Contains("no stored content for fact:spec", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_case_written_with_the_line_writer_reads_back_the_same()
    {
        var options = Options();
        var original = Case();

        var text = EvalLines.Write(original, options);

        Assert.Equal(string.Join('\n', _lines) + "\n", text);
        Assert.Equal(original.Steps.Count, EvalCaseReader.Read("happy", text, options).Case?.Steps.Count);
    }

    private static EvalCase Case() => Assert.IsType<EvalCase>(EvalCaseReader.Read("happy", string.Join('\n', _lines), Options()).Case);

    /// <summary>What each line's trigger reported, as the interpreter saves it: event i + 1 is line i + 2's trigger.</summary>
    private static Dictionary<TriggerKey, IReadOnlyDictionary<FactKey, FactValue>> Reported(EvalCase evalCase, IReadOnlyList<InstanceChange> history) =>
        history.Skip(1).Zip(evalCase.Steps).ToDictionary(
            static p => p.First.Event.Trigger.Key,
            static p => (IReadOnlyDictionary<FactKey, FactValue>)(p.Second.Trigger.Outputs ?? new Dictionary<FactKey, JsonElement>())
                .ToDictionary(static o => o.Key, static o => EvalFacts.Value(o.Value)));

    /// <summary>The stored content for every fact the case writes, by reference.</summary>
    private static Dictionary<string, JsonElement> Contents(EvalCase evalCase)
    {
        var all = evalCase.Start.Start.Inputs.Values.Concat(evalCase.Steps.SelectMany(s => s.Trigger.Outputs?.Values ?? []));
        var contents = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var content in all)
        {
            contents[EvalFacts.Value(content).Ref] = content;
        }

        return contents;
    }

    private static JsonSerializerOptions Options() => EvalComposition.Options;
}
