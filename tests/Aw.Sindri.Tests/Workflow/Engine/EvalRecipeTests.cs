using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Evals;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.EvalRecipe;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// The P3 signal (spec Phase 3, §9.2, §10 'Replay reproduces the instance'): 'sindri recipe eval
/// recipes/sindri/workflow.step.yaml' folds the engine's decisions over each shipped case and prints 'happy pass',
/// 'no-progress pass' and 'rounds pass'; a diverging line names its file and line; '--scaffold' prints a case that passes.
/// </summary>
public sealed class EvalRecipeTests
{
    private const string Step = "sindri/workflow.step.yaml";
    private const string StepHappy = "sindri/evals/workflow.step/happy.jsonl";
    private const string Start = """{"start":{"inputs":{"fact:spec":"Add a --version flag."}}}""";
    private const string Done = """{"trigger":{"kind":"step.completed","outcome":"done","outputs":{"fact:output":"Added --version."}},"expect":{"state":"checks"}}""";
    private const string Failed = """{"trigger":{"kind":"step.completed","outcome":"failed","outputs":{"fact:findings":"printed nothing"}},"expect":{"state":"produce"}}""";
    private const string FailedAgain = """{"trigger":{"kind":"step.completed","outcome":"failed","outputs":{"fact:findings":"printed nothing"}},"expect":{"state":"parked","park":"no-progress"}}""";

    private const string AskStart = """{"start":{"inputs":{}}}""";

    private readonly InMemoryRecipeFiles _files = Shipped();

    [Fact, Trait("Lane", "unit")]
    public async Task Eval_cases_reach_their_expected_states()
    {
        var response = await Eval(Step);

        Assert.Equal(["happy pass", "no-progress pass", "rounds pass"], response.Cases.Select(c => c.ToLine()), StringComparer.Ordinal);
        Assert.True(response.Passed);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("sindri/workflow.item.yaml", "happy pass|child-parks pass|claim-exists pass")]
    [InlineData("dev/workflow.dev-timeout.yaml", "happy pass")]
    [InlineData("dev/workflow.dev-notify.yaml", "happy pass")]
    public async Task Every_shipped_recipe_passes_its_evals_including_child_lines(string recipe, string lines)
    {
        var response = await Eval(recipe);

        Assert.Equal(lines.Split('|'), response.Cases.Select(c => c.ToLine()), StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(RecipesWithEvals))]
    public async Task Every_shipped_recipe_with_evals_passes_all_of_them(string recipe)
    {
        var response = await Eval(recipe);

        Assert.NotEmpty(response.Cases);
        Assert.True(response.Passed, string.Join('\n', response.Cases.Select(c => c.ToLine())));
    }

    public static TheoryData<string> RecipesWithEvals =>
        [.. ShippedRecipes.Recipes.Where(path => RecipeComposition.ReadClean(ShippedRecipes.Read(path), Path.GetFileName(path)).Recipe.Evals.Count > 0)];

    [Fact, Trait("Lane", "unit")]
    public async Task A_diverging_expect_names_the_eval_file_and_the_line_with_expected_and_actual()
    {
        var lines = ShippedRecipes.Read(StepHappy).Split('\n');
        lines[2] = lines[2].Replace("\"state\":\"verify\"", "\"state\":\"checks\"", StringComparison.Ordinal);
        _ = _files.With(StepHappy, string.Join('\n', lines));

        var response = await Eval(Step);

        Assert.False(response.Passed);
        Assert.Equal("happy fail at evals/workflow.step/happy.jsonl:3: expected checks, got verify", response.Cases[0].ToLine());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_eval_case_passes_through_an_answer_wait_on_a_text_answer()
    {
        const string recipe = "dev/workflow.ask.yaml";
        _ = _files.With(recipe, AnswerWaitTests.Yaml + "evals: [evals/workflow.ask/happy.jsonl]\n");
        _ = _files.With("dev/evals/workflow.ask/happy.jsonl", AskStart + "\n" + """{"trigger":{"kind":"answer","variant":"text","outputs":{"fact:answer":"Only the CLI."}},"expect":{"state":"done","outcome":"resolved"}}""" + "\n");

        var response = await Eval(recipe);

        Assert.Equal(["happy pass"], response.Cases.Select(c => c.ToLine()), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Scaffold_answers_an_answer_wait_with_a_text_line_that_then_passes()
    {
        const string recipe = "dev/workflow.ask.yaml";
        _ = _files.With(recipe, AnswerWaitTests.Yaml + "evals: [evals/workflow.ask/happy.jsonl]\n");

        var scaffold = Assert.IsType<string>((await Eval(recipe, EvalMode.Scaffold)).Scaffold);

        var printed = scaffold.TrimEnd('\n').Split('\n');
        Assert.Equal(2, printed.Length);
        Assert.Contains("\"variant\":\"text\"", printed[1], StringComparison.Ordinal);
        _ = _files.With("dev/evals/workflow.ask/happy.jsonl", scaffold);
        Assert.Equal(["happy pass"], (await Eval(recipe)).Cases.Select(c => c.ToLine()), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_wait_satisfied_line_on_an_answer_wait_says_to_answer_with_text()
    {
        const string recipe = "dev/workflow.ask.yaml";
        _ = _files.With(recipe, AnswerWaitTests.Yaml + "evals: [evals/workflow.ask/happy.jsonl]\n");
        _ = _files.With("dev/evals/workflow.ask/happy.jsonl", AskStart + "\n" + """{"trigger":{"kind":"wait.satisfied","outcome":"answered"},"expect":{"state":"done","outcome":"resolved"}}""" + "\n");

        var failed = (await Eval(recipe)).Cases[0];

        Assert.False(failed.Passed);
        Assert.Contains("'variant': 'text'", failed.ToLine(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_recipe_without_evals_is_the_empty_state()
    {
        _ = _files.With(Step, ShippedRecipes.Read(Step).Replace("evals: [evals/workflow.step/happy.jsonl, evals/workflow.step/no-progress.jsonl, evals/workflow.step/rounds.jsonl]\n", string.Empty, StringComparison.Ordinal));

        var response = await Eval(Step);

        Assert.Empty(response.Cases);
        Assert.True(response.Passed);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"trigger":{"kind":"step.done"},"expect":{"state":"checks"}}""", "'step.done' isn't a trigger an eval line can name")]
    [InlineData("""{"trigger":{"kind":"takeover","variant":"grant"},"expect":{"state":"taken-over"}}""", "variant 'grant' doesn't go with kind 'takeover'")]
    [InlineData("""{"trigger":{"kind":"answer"},"expect":{"state":"produce"}}""", "an answer line needs 'variant'")]
    [InlineData("""{"trigger":{"kind":"step.completed","outcome":"done","park":"rounds"},"expect":{"state":"checks"}}""", "'park' belongs on a child.parked line only")]
    [InlineData("""{"trigger":{"kind":"step.completed","outcome":"done"},"gates":{"gate.route.startable":"pass"},"expect":{"state":"checks"}}""", "'gates' names gate.route.startable")]
    [InlineData("""{"trigger":{"kind":"step.completed","outcome":"shipped"},"expect":{"state":"checks"}}""", "step.completed was rejected: SND-WF-008")]
    [InlineData("""{"trigger":"step.completed","expect":{"state":"checks"}}""", "not a trigger line")]
    public async Task A_line_the_engine_cannot_fold_fails_at_that_line(string line, string detail)
    {
        _ = _files.With(StepHappy, Start + "\n" + line + "\n");

        var failed = (await Eval(Step)).Cases[0];

        Assert.False(failed.Passed);
        Assert.Equal(2, failed.Line);
        Assert.Contains(detail, failed.Detail, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"trigger":{"kind":"step.completed","outcome":"done",""", 2)]
    [InlineData("""{"start":{"inputs":""", 1)]
    public async Task A_line_that_isnt_JSON_fails_at_that_line_without_the_parser_internals(string bad, int line)
    {
        var text = line == 1 ? bad + "\n" : Start + "\n" + bad + "\n";
        _ = _files.With(StepHappy, text);

        var failed = (await Eval(Step)).Cases[0];

        Assert.Equal((false, line), (failed.Passed, failed.Line));
        Assert.DoesNotContain("BytePositionInLine", failed.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("LineNumber", failed.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("EvalStepLine", failed.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("EvalStartLine", failed.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Grants_takeovers_and_cancels_are_written_as_evals()
    {
        string[] lines =
        [
            Start, Done, Failed, Done, FailedAgain,
            """{"trigger":{"kind":"answer","variant":"grant","amount":1},"expect":{"state":"produce"}}""",
            """{"trigger":{"kind":"takeover","variant":"takeover"},"expect":{"state":"taken-over"}}""",
            """{"trigger":{"kind":"handback"},"expect":{"state":"produce"}}""",
            Done,
            """{"trigger":{"kind":"cancel"},"expect":{"state":"cancelled","outcome":"cancelled"}}""",
        ];
        _ = _files.With(StepHappy, string.Join('\n', lines) + "\n");

        Assert.Equal("happy pass", (await Eval(Step)).Cases[0].ToLine());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Text_on_no_progress_resumes_again_on_a_second_park()
    {
        string[] lines =
        [
            Start, Done, Failed, Done, FailedAgain,
            """{"trigger":{"kind":"answer","variant":"text","outputs":{"fact:answer":"Read the version from the assembly."}},"expect":{"state":"produce"}}""",
            Done, FailedAgain,
            """{"trigger":{"kind":"answer","variant":"text"},"expect":{"state":"produce"}}""",
        ];
        _ = _files.With(StepHappy, string.Join('\n', lines) + "\n");

        var result = (await Eval(Step)).Cases[0];

        Assert.Equal("happy pass", result.ToLine());   // owner q93: a second text answer resumes the loop again
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("dev/workflow.dev-timeout.yaml", "dev/evals/workflow.dev-timeout/happy.jsonl", 2)]
    [InlineData(Step, StepHappy, 4)]
    public async Task Scaffold_prints_a_case_marked_scaffold_that_then_passes(string recipe, string eval, int lines)
    {
        var scaffold = (await Eval(recipe, EvalMode.Scaffold)).Scaffold;

        var printed = Assert.IsType<string>(scaffold).TrimEnd('\n').Split('\n');
        Assert.Equal(lines, printed.Length);
        Assert.Contains("\"scaffold\":true", printed[0], StringComparison.Ordinal);
        EvalFormat.AssertStart(printed[0]);
        Assert.All(printed.Skip(1), EvalFormat.AssertStep);
        _ = _files.With(eval, scaffold);
        Assert.True((await Eval(recipe)).Cases[0].Passed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_dev_evidence_route_passes_a_case_per_outcome()
    {
        var response = await Eval(Choice.RouteRecipes.Dev);

        Assert.Equal(
            ["happy pass", "api pass", "db pass", "all pass", "none pass", "other pass", "uncertain pass", "failed pass"],
            response.Cases.Select(c => c.ToLine()), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_eval_line_without_fact_decision_gets_the_eval_placeholder()
    {
        // the happy case never gives fact:decision, yet its transition produces it: the engine fills it as the desk does, with provider "eval"
        var happy = ShippedRecipes.Read("dev/evals/workflow.dev-evidence-route/happy.jsonl");
        Assert.DoesNotContain("fact:decision", happy, StringComparison.Ordinal);

        var response = await Eval(Choice.RouteRecipes.Dev);

        Assert.True(response.Cases[0].Passed, response.Cases[0].ToLine());
        var recipe = EvalComposition.Recipe(Choice.RouteRecipes.Dev);
        var line = new EvalStepLine(new EvalTrigger(TriggerNames.StepCompleted, OutcomeName.From("ui")), new EvalExpectation("evidence-ui"));
        var started = EvalSimulation.Start(new EvalCase("c", new EvalStartLine(new EvalStart(recipe.Inputs.ToDictionary(static i => i.Key, static i => EvalFacts.OfText("x"))), Scaffold: false), []), recipe, EvalComposition.Catalog, InMemoryChildRecipes.None).Value;
        var stepped = started.Step(line, 2).Value.Next;
        Assert.Equal(EvalFacts.Value(new DecisionFact("ui", "eval")), stepped.Instance.Facts[FactKey.From("fact:decision")]);
        Assert.Contains("fact:decision", recipe.Transitions[0].Produces.Select(p => p.Fact.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_step_completed_line_that_writes_the_engine_filled_output_on_the_desk_twin_is_refused()
    {
        var recipe = EvalComposition.Recipe(Choice.RouteRecipes.Dev);
        var start = new EvalStartLine(new EvalStart(recipe.Inputs.ToDictionary(static i => i.Key, static i => EvalFacts.OfText("x"))), Scaffold: false);
        var atDesk = EvalSimulation.Start(new EvalCase("c", start, []), recipe, EvalComposition.Catalog, InMemoryChildRecipes.None).Value
            .Step(new EvalStepLine(new EvalTrigger(TriggerNames.StepFailed), new EvalExpectation("route-evidence-desk")), 2).Value.Next;
        var written = new Dictionary<FactKey, System.Text.Json.JsonElement> { [FactKey.From("fact:decision")] = EvalFacts.OfText("db") };
        var line = new EvalStepLine(new EvalTrigger(TriggerNames.StepCompleted, OutcomeName.From("ui"), written), new EvalExpectation("evidence-ui"));

        var refused = atDesk.Step(line, 3);

        Assert.True(refused.IsError);
        Assert.Equal("fact:decision is written by the engine on 'step.desk.choose' (fix: drop --output fact:decision)", refused.Error.Message);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_deadline_line_on_a_park_state_fires_its_timeout_and_parks()
    {
        var response = await EvalTimed("park", ["""{"trigger":{"kind":"deadline"},"expect":{"state":"parked","park":"timeout"}}"""]);

        Assert.Equal(["happy pass"], response.Cases.Select(c => c.ToLine()), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Two_deadline_lines_on_a_retry_state_stay_in_the_state_once_and_then_park()
    {
        var response = await EvalTimed("retry",
        [
            """{"trigger":{"kind":"deadline"},"expect":{"state":"waiting"}}""",
            """{"trigger":{"kind":"deadline"},"expect":{"state":"parked","park":"timeout"}}""",
        ]);

        Assert.Equal(["happy pass"], response.Cases.Select(c => c.ToLine()), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_deadline_line_while_taken_over_fires_the_idle_reminder_and_changes_nothing()
    {
        var response = await EvalTimed("park",
        [
            """{"trigger":{"kind":"takeover","variant":"takeover"},"expect":{"state":"taken-over"}}""",
            """{"trigger":{"kind":"deadline"},"expect":{"state":"taken-over"}}""",
            """{"trigger":{"kind":"handback"},"expect":{"state":"waiting"}}""",
        ]);

        Assert.Equal(["happy pass"], response.Cases.Select(c => c.ToLine()), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_deadline_line_with_no_running_timer_fails_at_that_line()
    {
        var response = await EvalTimed("park",
        [
            """{"trigger":{"kind":"deadline"},"expect":{"state":"parked","park":"timeout"}}""",
            """{"trigger":{"kind":"deadline"},"expect":{"state":"parked","park":"timeout"}}""",
        ]);

        var failed = response.Cases[0];
        Assert.Equal((false, 3), (failed.Passed, failed.Line));
        Assert.Contains("no running timer", failed.Detail, StringComparison.Ordinal);
    }

    /// <summary>Evals a wait state that times out after 10m with 'then: <paramref name="then"/>' over the given lines.</summary>
    private async Task<EvalRecipeResponse> EvalTimed(string then, string[] lines)
    {
        const string recipe = "dev/workflow.timed.yaml";
        _ = _files.With(recipe, string.Join('\n',
        [
            "schema: 1",
            "id: workflow.timed",
            "version: 1",
            "inputs: [{ key: fact:item, type: item, kind: item }]",
            "initial: waiting",
            "states:",
            "  waiting: { kind: wait, waitFor: { event: item.routed }, timeout: { after: 10m, then: " + then + " },",
            "             next: \"Waiting for the router to route {item}.\" }",
            "  done:    { kind: terminal, outcome: resolved }",
            "transitions:",
            "  - { from: waiting, to: done, on: wait.satisfied }",
            "evals: [evals/workflow.timed/happy.jsonl]",
            string.Empty,
        ]));
        _ = _files.With("dev/evals/workflow.timed/happy.jsonl", AskStart + "\n" + string.Join('\n', lines) + "\n");
        return await Eval(recipe);
    }

    private const string Parent = "dev/workflow.dev-child-outputs.yaml";
    private const string ParentHappy = "dev/evals/workflow.dev-child-outputs/happy.jsonl";
    private const string ParentStart = """{"start":{"inputs":{"fact:request":"Export the weekly summary."}}}""";
    private const string ChildResolved = """{"trigger":{"kind":"wait.satisfied","outcome":"resolved","outputs":{"fact:output":"summary-week-41.csv"}},"expect":{"state":"review"}}""";
    private const string Accepted = """{"trigger":{"kind":"step.completed","outcome":"accepted"},"expect":{"state":"done","outcome":"resolved"}}""";

    [Fact, Trait("Lane", "unit")]
    public async Task A_child_lines_outputs_reach_the_parent()
    {
        // accepted reaches 'done' only if the renamed fact:exported arrived, since that branch tests it
        _ = _files.With(ParentHappy, string.Join('\n', ParentStart, ChildResolved, Accepted) + "\n");

        var response = await Eval(Parent);

        Assert.True(response.Passed, string.Join('\n', response.Cases.Select(c => c.ToLine())));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_child_line_without_the_output_takes_the_other_branch()
    {
        const string NoOutput = """{"trigger":{"kind":"wait.satisfied","outcome":"resolved"},"expect":{"state":"review"}}""";
        const string Unproven = """{"trigger":{"kind":"step.completed","outcome":"accepted"},"expect":{"state":"unproven","outcome":"unresolved"}}""";
        _ = _files.With(ParentHappy, string.Join('\n', ParentStart, NoOutput, Unproven) + "\n");

        var response = await Eval(Parent);

        Assert.True(response.Passed, string.Join('\n', response.Cases.Select(c => c.ToLine())));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_child_line_with_an_undeclared_output_fails_naming_it()
    {
        const string Undeclared = """{"trigger":{"kind":"wait.satisfied","outcome":"resolved","outputs":{"fact:findings":"x"}},"expect":{"state":"review"}}""";
        _ = _files.With(ParentHappy, string.Join('\n', ParentStart, Undeclared) + "\n");

        var failed = (await Eval(Parent)).Cases[0];

        Assert.False(failed.Passed);
        Assert.Equal(2, failed.Line);
        Assert.Contains("wait.satisfied from child 'workflow.dev-child-summary' gives fact:findings, which workflow.dev-child-summary doesn't declare as outputs (fix: give only its outputs: fact:output)", failed.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_child_line_with_outputs_from_a_child_that_declares_none_says_so()
    {
        const string ChildFile = "dev/workflow.dev-child-summary.yaml";
        const string Declared = "  - { key: fact:output, label: \"Exported file\", type: file, from: export }\n";
        var child = ShippedRecipes.Read(ChildFile);
        Assert.Contains(Declared, child, StringComparison.Ordinal);
        _ = _files.With(ChildFile, child.Replace("outputs:\n" + Declared, "outputs: []\n", StringComparison.Ordinal));
        _ = _files.With(ParentHappy, string.Join('\n', ParentStart, ChildResolved) + "\n");

        var failed = (await Eval(Parent)).Cases[0];

        Assert.False(failed.Passed);
        Assert.Contains("which workflow.dev-child-summary doesn't declare as outputs (fix: remove outputs; workflow.dev-child-summary declares none)", failed.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_cancelled_child_line_with_outputs_fails()
    {
        const string Cancelled = """{"trigger":{"kind":"wait.satisfied","outcome":"cancelled","outputs":{"fact:output":"x"}},"expect":{"state":"dropped","outcome":"unresolved"}}""";
        _ = _files.With(ParentHappy, string.Join('\n', ParentStart, Cancelled) + "\n");

        var failed = (await Eval(Parent)).Cases[0];

        Assert.False(failed.Passed);
        Assert.Contains("a cancelled child carries no outputs (fix: remove outputs from this line)", failed.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Scaffold_gives_a_child_line_the_outputs_its_outcome_always_carries()
    {
        var scaffold = Assert.IsType<string>((await Eval(Parent, EvalMode.Scaffold)).Scaffold);

        Assert.Contains("\"fact:output\":\"fact:output from workflow.dev-child-summary (resolved)\"", scaffold, StringComparison.Ordinal);
        _ = _files.With(ParentHappy, scaffold);
        Assert.True((await Eval(Parent)).Passed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Scaffold_of_workflow_item_is_unchanged()
    {
        var scaffold = Assert.IsType<string>((await Eval("sindri/workflow.item.yaml", EvalMode.Scaffold)).Scaffold);

        Assert.DoesNotContain("workflow.step (", scaffold, StringComparison.Ordinal);
        Assert.DoesNotContain("\"kind\":\"wait.satisfied\",\"outcome\":\"resolved\",\"outputs\"", scaffold, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_missing_eval_file_is_a_tool_error()
    {
        var files = new InMemoryRecipeFiles().With(Step, ShippedRecipes.Read(Step));

        var result = await Service(files).Handle(new EvalRecipeRequest(Step), TestContext.Current.CancellationToken);

        Assert.Equal(WorkflowErrors.RecipeEvalsFailing.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_eval_outside_the_recipe_directory_is_refused()
    {
        _ = _files.With(Step, ShippedRecipes.Read(Step).Replace("evals/workflow.step/happy.jsonl,", "../evals/happy.jsonl,", StringComparison.Ordinal));

        var result = await Service(_files).Handle(new EvalRecipeRequest(Step), TestContext.Current.CancellationToken);

        Assert.Contains("leaves the recipe's directory", result.Error.Message, StringComparison.Ordinal);
    }

    private async Task<EvalRecipeResponse> Eval(string recipe, EvalMode mode = EvalMode.Run) =>
        (await Service(_files).Handle(new EvalRecipeRequest(recipe, mode), TestContext.Current.CancellationToken)).Value;

    private static InMemoryRecipeFiles Shipped()
    {
        var files = new InMemoryRecipeFiles();
        foreach (var path in ShippedRecipes.Paths)
        {
            _ = files.With(path, ShippedRecipes.Read(path));
        }

        return files;
    }

    private static EvalRecipeService Service(InMemoryRecipeFiles files) => EvalComposition.Service(files);
}
