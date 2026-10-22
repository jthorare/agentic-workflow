using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>workflow.review-loop version 2 (line spec DTC4; plan LN-Y2 U4): the lens frame, one lens per run, a recorded review packet.</summary>
public sealed class ReviewLoopV2Tests
{
    private const string Path = "sindri/workflow.review-loop.yaml";

    private static Recipe Load() => RecipeComposition.ReadClean(ShippedRecipes.Read(Path), "workflow.review-loop.yaml").Recipe;

    [Fact, Trait("Lane", "unit")]
    public void The_recipe_is_version_5_and_validates_against_the_shipped_catalog()
    {
        var document = RecipeComposition.ReadClean(ShippedRecipes.Read(Path), "workflow.review-loop.yaml");

        Assert.Equal(5, document.Recipe.Version);   // 5: the phase worktree is an input (LN-WF2); 4: the round log is an input, so round 1 holds fact:lensRounds (LN-PK lite); the typed escalation (BS-12) and the display fields (SO-E1) came with it
        Assert.Empty(RecipeComposition.Errors(document, FixtureCatalog.Shipped(), new InMemoryChildRecipes()));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_review_output_is_a_packet_written_by_the_record_state()
    {
        var output = Assert.Single(Load().Outputs);

        Assert.Equal("fact:review", output.Key.Value);
        Assert.Equal("packet", output.Type);
        Assert.Equal("record", output.From);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_inputs_are_the_change_the_phase_packet_the_round_log_and_the_phase_worktree()
        => Assert.Equal(["fact:change", "fact:phasePacket", "fact:lensRounds", "fact:workspace"], Load().Inputs.Select(static i => i.Key.Value), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void The_phase_starts_each_review_with_an_empty_round_log()
    {
        var phase = RecipeComposition.ReadClean(ShippedRecipes.Read("sindri/workflow.phase.yaml"), "workflow.phase.yaml").Recipe;

        var inputs = phase.States["review"].WaitFor!.Child!.Inputs;   // why: 'review' is the phase's child wait

        Assert.Equal(4, phase.Version);   // 4: the owner's stop reports the phase failed to its line (LN-WF1); 3: passes the phase worktree (LN-WF2); 2: passes the empty round log (LN-PK lite)
        var log = inputs[FactKey.From("fact:lensRounds")];
        Assert.Equal("[]", log.Literal!.Value.GetRawText());
        Assert.Null(log.Fact);
        Assert.Equal(FactKey.From("fact:workspace"), inputs[FactKey.From("fact:workspace")].Fact);   // the loop's agent Steps launch in the phase worktree
    }

    [Fact, Trait("Lane", "unit")]
    public void The_graph_runs_round_lens_log_and_ends_clean_through_record()
    {
        var recipe = Load();

        Assert.Equal("round", recipe.Initial);
        Assert.Equal(
            ["ask", "clean", "fix", "lens", "lens-log", "recheck", "record", "resolve", "round", "stuck", "wrong"],
            recipe.States.Keys.Order(StringComparer.Ordinal),
            StringComparer.Ordinal);
        Assert.Equal("step.review.round", recipe.States["round"].Step!.Use.Value);
        Assert.Equal("step.lens.run", recipe.States["lens"].Step!.Use.Value);
        Assert.Equal("step.review.log", recipe.States["lens-log"].Step!.Use.Value);
        Assert.Equal("step.review.record", recipe.States["record"].Step!.Use.Value);
        Assert.Equal("step.review.fix", recipe.States["fix"].Step!.Use.Value);
        Assert.Equal("step.review.recheck", recipe.States["recheck"].Step!.Use.Value);
        Assert.Equal("rounds", recipe.States["round"].Loop);
        Assert.Equal(5, recipe.Loops["rounds"].Cap);
        Assert.Equal("change", recipe.States["lens"].Step!.With["subject"].Literal?.GetString());
        Assert.Equal("lenses", recipe.States["lens"].Step!.With["bundle"].Literal?.GetString());
    }

    [Fact, Trait("Lane", "unit")]
    public void Clean_is_reached_only_through_record_so_the_packet_is_always_frozen()
    {
        var into = Load().Transitions.Where(static t => t.To == "clean").ToList();

        Assert.All(into, static t => Assert.Equal(["record"], t.From));
        Assert.DoesNotContain(Load().Transitions, static t => t.To == "record" && t.From.Contains("round", StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void Failed_is_reached_only_through_record_so_a_failed_review_also_carries_its_frozen_packet()
    {
        var into = Load().Transitions.Where(static t => t.To == "wrong").ToList();

        Assert.Equal(["record"], Assert.Single(into).From);
        Assert.Contains(Load().Transitions, static t => t.To == "record" && t.From.Contains("lens-log", StringComparer.Ordinal) && t.When?.Outcome?.Value == "behaviourWrong");
        Assert.Contains(Load().Transitions, static t => t.To == "record" && t.From.Contains("recheck", StringComparer.Ordinal) && t.When?.Outcome?.Value == "behaviourWrong");
        Assert.Contains(into, static t => t.Produces.Any(static p => p.Fact.Value == "fact:review"));
    }

    [Fact, Trait("Lane", "unit")]
    public void No_lens_applying_ends_stuck_for_the_owner_and_never_enters_the_lens_loop()
    {
        var fromRound = Load().Transitions.Where(static t => t.From.Contains("round", StringComparer.Ordinal)).ToList();

        Assert.Contains(fromRound, static t => t.To == "stuck" && t.When?.Outcome?.Value == "noLens");
        Assert.Equal(50, Load().Loops["lens-runs"].Cap);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_step_id_is_in_a_reserved_family()
    {
        var ids = Load().States.Values.Where(static s => s.Step is not null).Select(static s => s.Step!.Use.Value);

        Assert.All(ids, static id => Assert.DoesNotMatch(@"^(step\.judge\.|step\.desk\.|input\.)", id));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_lens_frame_takes_a_subject_and_a_bundle_and_reports_one_lens_per_run()
    {
        var run = FixtureCatalog.Shipped().Find(BlockId.From("step.lens.run"))!;

        Assert.Equal(BlockRuntime.Agent, run.Runtime);
        Assert.Equal(["reviewed"], run.Outcomes.Select(static o => o.Value), StringComparer.Ordinal);
        Assert.Equal(["subject"], run.Parameters.Where(static p => p.Required).Select(static p => p.Name), StringComparer.Ordinal);
        Assert.Contains(run.Parameters, static p => p.Name == "bundle");
    }

    [Fact, Trait("Lane", "unit")]
    public void The_replaced_review_run_step_is_gone_and_the_new_review_steps_are_shipped()
    {
        var catalog = FixtureCatalog.Shipped();

        Assert.Null(catalog.Find(BlockId.From("step.review.run")));
        Assert.All(["step.review.round", "step.review.log", "step.review.record", "step.lens.run"], id => Assert.NotNull(catalog.Find(BlockId.From(id))));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_eval_names_of_version_1_are_kept_and_the_lens_cases_are_added()
    {
        var names = Load().Evals.Select(static e => System.IO.Path.GetFileNameWithoutExtension(e));

        Assert.Equal(["ambiguous-fix-escalates", "behaviour-wrong", "cap-then-recheck", "empty-queue", "happy", "lens-runs-cap", "one-lens-per-run", "product-question-escalates", "recheck-clean", "recheck-wrong", "two-rounds-no-human", "untyped-please-decide-stays"], names.Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_step_names_a_step_input_so_approval_cannot_refuse_it_with_SND_WF_016() =>
        Assert.All(Load().States.Values.Where(static s => s.Step is not null), static s => Assert.Empty(s.Step!.Inputs));
}
