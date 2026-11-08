using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Tests.GetInstance;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.Layout;

/// <summary>CurrentStates: the active state's step declaration, the gates on its exits and the desk form (API spec §3.8 'current').</summary>
[Lane(TestLane.Unit)]
public sealed class CurrentStatesTests
{
    private static readonly FactKey _output = FactKey.From("fact:output");
    private static readonly FactKey _notes = FactKey.From("fact:notes");

    private static InterpretedRecipe Interpreted(Recipe recipe) => StatusCases.Recipe with { Recipe = recipe };

    private static RecipeTransition Leaving(string from, params GateUse[] gates) =>
        new([from], "done", "step.completed", When: null, gates, [], [], []);

    private static GateUse Gate(string id, double? threshold = null) => new(BlockId.From(id), threshold, new Dictionary<string, WithValue>(StringComparer.Ordinal));

    private static DeskTask Task(IReadOnlyList<OutcomeName> outcomes, params OutputSpec[] outputs) =>
        new(RunToken.For(1, 1), "produce", BlockId.From("step.produce"), outcomes, outputs, new Dictionary<FactKey, InputFact>(), TimeoutAt: null);

    [Fact]
    public void A_state_that_runs_a_step_is_an_active_step_named_by_its_block()
    {
        var step = CurrentStates.Step(EngineFixture.At(EngineFixture.Step, "produce"), Interpreted(EngineFixture.Step));

        Assert.Equal(new StepDeclaration("active", "step.produce", "step.produce"), step);
    }

    [Fact]
    public void A_state_that_waits_for_a_child_is_a_wait_step_named_by_the_child_recipe()
    {
        var step = CurrentStates.Step(EngineFixture.At(EngineFixture.Item, "running"), Interpreted(EngineFixture.Item));

        Assert.Equal(new StepDeclaration("wait", "workflow.step", "one child per packet task"), step);
    }

    [Fact]
    public void A_state_that_waits_for_an_answer_is_a_wait_step_named_by_the_state()
    {
        var recipe = EngineFixture.Step with
        {
            States = new Dictionary<string, RecipeState>(EngineFixture.Step.States, StringComparer.Ordinal)
            {
                ["produce"] = EngineFixture.Step.States["produce"] with { Step = null, WaitFor = new WaitFor(Event: null, new AnswerWait(AnswerId.From("severity"), "How severe?"), Resource: null, Child: null) },
            },
        };

        var step = CurrentStates.Step(EngineFixture.At(EngineFixture.Step, "produce"), Interpreted(recipe));

        Assert.Equal(new StepDeclaration("wait", "produce", "waits for an answer"), step);
    }

    [Fact]
    public void A_terminal_state_has_no_step_and_a_state_the_recipe_lacks_has_none_either()
    {
        Assert.Null(CurrentStates.Step(EngineFixture.At(EngineFixture.Step, "done"), Interpreted(EngineFixture.Step)));
        Assert.Null(CurrentStates.Step(EngineFixture.At(EngineFixture.Step, "produce") with { Frame = new Core.Workflow.Engine.Instances.Frame("nowhere", 1, Run: null, Child: null) }, Interpreted(EngineFixture.Step)));
    }

    [Fact]
    public void A_gate_with_a_threshold_says_at_what_score_it_passes_and_one_without_just_that_it_passes()
    {
        var recipe = EngineFixture.Step with { Transitions = [Leaving("produce", Gate("gate.review.judge", 0.8), Gate("gate.test.check"))] };

        var gates = CurrentStates.Gates(EngineFixture.At(EngineFixture.Step, "produce"), Interpreted(recipe));

        Assert.Equal(
            [
                new GateLine(BlockId.From("gate.review.judge"), "exit", 0.8, "gate.review.judge passes at 0.8 or above."),
                new GateLine(BlockId.From("gate.test.check"), "exit", Threshold: null, "gate.test.check passes."),
            ],
            gates);
    }

    [Fact]
    public void A_gate_on_two_exits_is_listed_once_and_only_the_exits_of_the_active_state_count()
    {
        var recipe = EngineFixture.Step with
        {
            Transitions = [Leaving("produce", Gate("gate.test.check")), Leaving("produce", Gate("gate.test.check")), Leaving("checks", Gate("gate.review.judge", 0.5))],
        };

        var gates = CurrentStates.Gates(EngineFixture.At(EngineFixture.Step, "produce"), Interpreted(recipe));

        Assert.Equal([BlockId.From("gate.test.check")], gates.Select(static g => g.Id));
    }

    [Fact]
    public void No_task_means_no_desk_form() =>
        Assert.Null(CurrentStates.Desk(task: null, Interpreted(EngineFixture.Step)));

    [Fact]
    public void The_outputs_are_exactly_those_required_on_the_reported_outcome_so_they_agree_with_the_command_and_the_checks()
    {
        var recipe = Interpreted(EngineFixture.Step with { Outputs = [new RecipeOutput(_output, "Output", "file", From: "produce"), new RecipeOutput(_notes, "Notes", "text", From: "produce")] });
        var done = OutcomeName.From("done");

        var form = CurrentStates.Desk(Task([done], new OutputSpec(_output, [done]), new OutputSpec(_notes, [])), recipe)!;

        Assert.Equal([new DeskOutput("fact:output", FactType.File, Required: true)], form.Outputs);
        Assert.Equal(["output.file"], form.Checks);
    }

    [Fact]
    public void An_output_required_only_on_a_later_outcome_is_not_listed_while_the_command_reports_the_first()
    {
        var recipe = Interpreted(EngineFixture.Step with { Outputs = [new RecipeOutput(_notes, "Findings", "text", From: "import")] });
        var passed = OutcomeName.From("passed");
        var failed = OutcomeName.From("failed");

        var form = CurrentStates.Desk(Task([passed, failed], new OutputSpec(_notes, [failed])), recipe)!;

        Assert.Empty(form.Outputs);
        Assert.Empty(form.Checks);
    }

    [Fact]
    public void The_type_of_an_output_is_the_recipes_an_unknown_name_is_payload_and_an_undeclared_one_is_the_default_text()
    {
        var recipe = Interpreted(EngineFixture.Step with { Outputs = [new RecipeOutput(_output, "Output", "mystery", From: "produce")] });
        var done = OutcomeName.From("done");

        var declared = CurrentStates.Desk(Task([done], new OutputSpec(_output, [done])), recipe)!.Outputs;
        var undeclared = CurrentStates.Desk(Task([done], new OutputSpec(_notes, [done])), recipe)!.Outputs;

        Assert.Equal(FactType.Payload, Assert.Single(declared).Type);
        Assert.Equal(FactType.Text, Assert.Single(undeclared).Type);   // engine spec §3.3: an undeclared type is text
    }

    [Fact]
    public void The_checks_are_one_per_output_required_on_the_reported_outcome_named_by_its_declared_type()
    {
        var recipe = Interpreted(EngineFixture.Step with
        {
            Outputs = [new RecipeOutput(_output, "Output", "file", From: "produce"), new RecipeOutput(_notes, "Notes", Type: null, From: "produce")],
        });
        var done = OutcomeName.From("done");
        var failed = OutcomeName.From("failed");

        var checks = CurrentStates.Desk(Task([done, failed], new OutputSpec(_output, [done]), new OutputSpec(_notes, [done]), new OutputSpec(FactKey.From("fact:findings"), [failed])), recipe)!.Checks;

        Assert.Equal(["output.file", "notes.present"], checks);   // findings is required on 'failed' only, and the form reports 'done'
    }

    [Fact]
    public void A_step_that_declares_no_outcome_checks_every_required_output()
    {
        var recipe = Interpreted(EngineFixture.Step with { Outputs = [new RecipeOutput(_output, "Output", "text", From: "produce")] });

        var checks = CurrentStates.Desk(Task([], new OutputSpec(_output, [OutcomeName.From("done")]), new OutputSpec(_notes, [])), recipe)!.Checks;

        Assert.Equal(["output.text"], checks);
    }
}
