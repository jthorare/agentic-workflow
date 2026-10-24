using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Tests.GetInstance;
using Aw.Sindri.Tests.Workflow.Engine;
using ActionClass = Aw.Sindri.Core.Commands.ActionClass;
using FactTypes = Aw.Sindri.Core.Workflow.Engine.Runtime.FactTypes;

namespace Aw.Sindri.Tests.Layout;

/// <summary>
/// The producer of the layout members (UI screens E-20, E-23; API spec §3.8; P4b "status shows all lines", "fact producer
/// and time"): <see cref="InstanceViews.Of"/> over a real interpreted recipe, an incident one with a declared input, two
/// declared outputs and a fact with a producer, asserting every member it fills.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class InstanceViewProducerTests
{
    private static readonly FactKey _spec = FactKey.From("fact:spec");
    private static readonly FactKey _output = FactKey.From("fact:output");
    private static readonly FactKey _findings = FactKey.From("fact:findings");

    private static readonly InterpretedRecipe _incident = StatusCases.Recipe with
    {
        Recipe = StatusCases.Recipe.Recipe with
        {
            Urgency = "incident",
            Inputs = [new RecipeInput(_spec, "Spec", "file", "file", Generate: null)],
            Outputs = [new RecipeOutput(_output, "Output", "text", "produce"), new RecipeOutput(_findings, "Findings", Type: "mystery", "checks")],
        },
    };

    private static InstanceView Produce(Func<Core.Workflow.Engine.Instances.WorkflowInstance, Core.Workflow.Engine.Instances.WorkflowInstance>? with = null)
    {
        var instance = EngineFixture.At(_incident.Recipe, "produce");
        instance = instance with { Facts = new Dictionary<FactKey, FactValue> { [_spec] = EngineFixture.Fact("the spec") } };
        var provenance = new Dictionary<FactKey, FactProvenance> { [_spec] = new("human:thor", StatusCases.Now - TimeSpan.FromMinutes(90)) };
        return InstanceViews.Of(
            with?.Invoke(instance) ?? instance,
            _incident,
            StatusCases.Now - TimeSpan.FromMinutes(72),
            StatusCases.Now + TimeSpan.FromHours(3),
            new InstanceViewContext(Version: 4, Provenance: provenance));
    }

    [Fact]
    public void A_link_facts_ref_carries_its_url_host_and_title_as_the_subject_does()
    {
        var pr = FactKey.From("fact:pr");
        var recipe = _incident with { Recipe = _incident.Recipe with { Inputs = [new RecipeInput(pr, "PR", "link", "link", Generate: null)] } };
        var instance = EngineFixture.At(recipe.Recipe, "produce") with { Facts = new Dictionary<FactKey, FactValue> { [pr] = EngineFixture.Fact("link") } };
        var contents = new Dictionary<FactKey, string> { [pr] = "{\"url\":\"https://github.com/o/r/pull/9\",\"title\":\"Fix it\"}" };

        var view = InstanceViews.Of(instance, recipe, StatusCases.Now, timeoutAt: null, new InstanceViewContext(Contents: contents));

        var link = Assert.Single(view.Facts, static f => f.Key == "fact:pr").Ref!;
        Assert.Equal(("https://github.com/o/r/pull/9", "github.com", "Fix it", "fact:pr"), (link.Url, link.Host, link.Title, link.FactKey));
    }

    [Fact]
    public void The_identity_members_come_from_the_instance_and_the_recipe()
    {
        var view = Produce();

        Assert.Equal((ItemKey.From("T-3"), RecipeUrgency.Incident, PathOrdering.Path), (view.ItemId, view.Urgency, view.OrderedBy));
        Assert.Null(view.EngineOwned);
        Assert.Equal(new SubjectRef("file", "spec.md", "spec.md", Href: null), view.Subject);
        Assert.Equal([new InputPresence("fact:spec", "Spec", "file", Required: true, Present: true)], view.Inputs);
        Assert.Empty(view.ChildGroups);
    }

    [Fact]
    public void An_input_the_instance_does_not_hold_is_not_present()
    {
        var absent = InstanceViews.Of(
            EngineFixture.At(_incident.Recipe, "produce"), _incident with { Inputs = new Dictionary<FactKey, InputFact>() }, StatusCases.Now, timeoutAt: null, new InstanceViewContext());

        Assert.False(Assert.Single(absent.Inputs).Present);
        Assert.Equal("Spec", absent.Subject?.Label);   // the declared label, as no file name was given
        Assert.Equal("Spec", absent.Subject?.Key);
    }

    [Fact]
    public void The_state_cells_follow_the_path_order_numbered_from_1_with_the_terminal_last()
    {
        var cells = Produce().States;

        Assert.Equal(["produce", "checks", "verify", "done"], cells.Select(static c => c.State), StringComparer.Ordinal);
        Assert.Equal([1, 2, 3, 4], cells.Select(static c => c.Order ?? 0));
        Assert.Equal([StateGlyph.Active, StateGlyph.Pending, StateGlyph.Pending, StateGlyph.Pending], cells.Select(static c => c.Glyph));
        Assert.Equal([false, false, false, true], cells.Select(static c => c.Terminal));
    }

    [Fact]
    public void The_state_cells_are_built_from_the_recipes_path_order_not_a_second_walk()
    {
        var places = StatePathOrder.Of(_incident.Recipe).States;

        Assert.Equal(places.Select(static p => (p.State, p.Order, p.BranchOf)), Produce().States.Select(static c => (c.State, c.Order, c.BranchOf)));
    }

    [Fact]
    public void The_active_state_carries_the_round_the_loop_pairs_and_the_desk_mark()
    {
        var produce = Produce().States[0];

        Assert.Equal(new Round(1, 5), produce.Round);
        Assert.Equal([new LoopPair("verify", 0, 5), new LoopPair("checks", 0, 5)], produce.Loops);
        Assert.True(produce.Desk);
        Assert.False(produce.Child);
        Assert.Null(produce.Badge);
    }

    [Fact]
    public void A_loop_pair_counts_the_rounds_completed_so_far()
    {
        var view = Produce(i => i with { Loops = new Dictionary<string, Core.Workflow.Engine.Instances.LoopState>(StringComparer.Ordinal) { ["revisions"] = new(3, 5, Last: null) } });

        Assert.Equal(new Round(3, 5), view.States[0].Round);
        Assert.Equal([new LoopPair("verify", 2, 5), new LoopPair("checks", 2, 5)], view.States[0].Loops);
        Assert.Equal([new LoopPair("produce", 2, 5)], view.States[1].Loops);
    }

    [Fact]
    public void A_state_after_the_active_one_is_pending_and_one_before_it_is_done()
    {
        var view = InstanceViews.Of(EngineFixture.At(_incident.Recipe, "verify"), _incident, StatusCases.Now, timeoutAt: null, new InstanceViewContext());

        Assert.Equal([StateGlyph.Done, StateGlyph.Done, StateGlyph.Active, StateGlyph.Pending], view.States.Select(static c => c.Glyph));
    }

    [Fact]
    public void An_instance_parked_on_a_branch_state_after_a_loop_shows_every_state_it_visited_as_done()
    {
        var branching = Branching();
        var instance = EngineFixture.At(branching.Recipe, "review") with { Visited = ["produce", "checks", "verify", "review"] };   // produce, checks, verify, back to produce, checks, then the branch

        var view = InstanceViews.Of(instance, branching, StatusCases.Now, timeoutAt: null, new InstanceViewContext());

        Assert.Equal(
            [("produce", StateGlyph.Done), ("checks", StateGlyph.Done), ("verify", StateGlyph.Done), ("review", StateGlyph.Active), ("done", StateGlyph.Pending)],
            view.States.Select(static c => (c.State, c.Glyph)));
    }

    private static InterpretedRecipe Branching()
    {
        var plain = _incident.Recipe;
        return _incident with
        {
            Recipe = plain with
            {
                States = new Dictionary<string, RecipeState>(plain.States, StringComparer.Ordinal) { ["review"] = plain.States["checks"] },
                Transitions = [.. plain.Transitions, plain.Transitions[0] with { From = ["checks"], To = "review" }],
            },
        };
    }

    [Fact]
    public void A_finished_instance_that_visited_a_branch_state_shows_it_done_and_the_main_path_states_it_skipped_pending()
    {
        var branching = Branching();
        var instance = EngineFixture.At(branching.Recipe, "done") with
        {
            Outcome = OutcomeName.From("resolved"),
            Visited = ["produce", "checks", "review", "done"],
        };

        var view = InstanceViews.Of(instance, branching, StatusCases.Now, timeoutAt: null, new InstanceViewContext());

        Assert.Equal(
            [("produce", StateGlyph.Done), ("checks", StateGlyph.Done), ("verify", StateGlyph.Pending), ("review", StateGlyph.Done), ("done", StateGlyph.Done)],
            view.States.Select(static c => (c.State, c.Glyph)));
    }

    [Fact]
    public void A_finished_instance_has_its_terminal_state_done_with_the_outcome_and_every_main_path_state_done()
    {
        var view = StatusCases.View("done");

        Assert.Equal([StateGlyph.Done, StateGlyph.Done, StateGlyph.Done, StateGlyph.Done], view.States.Select(static c => c.Glyph));
        Assert.Equal("resolved", view.States[^1].Outcome);
    }

    [Theory]
    [InlineData("parked-rounds", EngineOwnedKind.Parked, "rounds")]
    [InlineData("escalated-gate", EngineOwnedKind.Escalated, "gate")]
    [InlineData("taken-over", EngineOwnedKind.TakenOver, "taken over by ")]
    public void An_interrupted_instance_has_its_engine_badge_on_the_state_it_is_in_and_on_the_view(string name, EngineOwnedKind kind, string reason)
    {
        var view = StatusCases.View(name);

        Assert.Equal(kind, view.EngineOwned?.Kind);
        Assert.StartsWith(reason, view.EngineOwned?.Reason, StringComparison.Ordinal);
        Assert.Equal(view.EngineOwned, Assert.Single(view.States, static c => c.Glyph == StateGlyph.Active).Badge);
    }

    [Fact]
    public void The_fact_rows_list_the_inputs_then_the_outputs_in_declaration_order_with_who_and_when_for_a_produced_one()
    {
        var facts = Produce().Facts;

        Assert.Equal(["fact:spec", "fact:output", "fact:findings"], facts.Select(static f => f.Key), StringComparer.Ordinal);
        var spec = facts[0];
        Assert.Equal((FactDirection.Input, FactStatus.Produced, FactType.File, "Spec"), (spec.Direction, spec.Status, spec.Type, spec.Label));
        Assert.Equal(("human:thor", StatusCases.Now - TimeSpan.FromMinutes(90), "spec.md"), (spec.By, spec.At, spec.Summary));
        Assert.Equal("spec.md", spec.Ref?.Name);
        Assert.Equal("sha256:" + EngineFixture.Fact("the spec").Sha256, spec.Ref?.Hash);   // every hash on the wire is sha256:<64 hex>
    }

    [Fact]
    public void An_output_the_open_desk_task_owes_is_pending_with_what_completing_it_needs_and_another_is_pending()
    {
        var facts = Produce().Facts;

        var output = facts[1];
        Assert.Equal((FactDirection.Output, FactStatus.Pending, FactType.Text, "produce"), (output.Direction, output.Status, output.Type, output.From));
        Assert.NotNull(output.Pending);
        Assert.Equal(Core.Ids.ActionId.From("CompleteStep"), output.Pending.CompleteAction);
        Assert.Equal((null, null, null), (output.At, output.By, output.Ref));

        var findings = facts[2];
        Assert.Equal((FactStatus.Pending, FactType.Payload, "checks", "not produced yet"), (findings.Status, findings.Type, findings.From, findings.Summary));   // an unknown type is served as payload
        Assert.Null(findings.Pending);
    }

    [Fact]
    public void A_recorded_output_is_produced_with_its_producer_and_a_short_digest_as_its_summary()
    {
        var value = EngineFixture.Fact("the output");
        var instance = EngineFixture.At(_incident.Recipe, "checks") with
        {
            Facts = new Dictionary<FactKey, FactValue> { [_spec] = EngineFixture.Fact("the spec"), [_output] = value },
        };
        var at = StatusCases.Now - TimeSpan.FromMinutes(5);
        var view = InstanceViews.Of(instance, _incident, at, timeoutAt: null,
            new InstanceViewContext(Provenance: new Dictionary<FactKey, FactProvenance> { [_output] = new("step.produce", at) }));

        var output = view.Facts[1];
        Assert.Equal((FactStatus.Produced, "step.produce", at), (output.Status, output.By, output.At));
        Assert.Equal($"sha256:{StatusText.Digest(value.Sha256)}", output.Summary);
        Assert.Null(output.Ref);   // a text fact has no handle (API spec 3.8 ref): the summary is its text
        Assert.Null(output.Pending);
    }

    [Fact]
    public void The_status_text_is_the_text_status_prints_and_the_header_is_its_first_line()
    {
        var view = Produce();

        Assert.Equal(StatusLayout.Render(view, StatusCases.Now), InstanceViews.Status(view, StatusCases.Now).Text);
        Assert.Equal(InstanceHeader.Line(view, StatusCases.Now), InstanceViews.Status(view, StatusCases.Now).Text.Split('\n')[0]);
    }

    [Fact]
    public void The_header_since_and_age_in_whole_seconds_are_those_of_the_state_entry()
    {
        var header = InstanceViews.Status(Produce(), StatusCases.Now).Header;

        Assert.Equal(
            new StatusHeader(InstanceAddress.From("T-3/step/1"), RecipeId.From("workflow.step"), StatusText.Hash12(EngineFixture.Step.Hash!.Value.Value), 4, "produce", "desk task", StatusCases.Now - TimeSpan.FromMinutes(72), 72 * 60),
            header);
    }

    [Fact]
    public void The_age_is_whole_seconds_and_never_negative()
    {
        var odd = InstanceViews.Of(EngineFixture.At(_incident.Recipe, "produce"), _incident, StatusCases.Now - TimeSpan.FromMilliseconds(61_900), timeoutAt: null, new InstanceViewContext());
        var future = InstanceViews.Of(EngineFixture.At(_incident.Recipe, "produce"), _incident, StatusCases.Now + TimeSpan.FromMinutes(1), timeoutAt: null, new InstanceViewContext());

        Assert.Equal((61L, 0L), (InstanceViews.Status(odd, StatusCases.Now).Header.AgeSeconds, InstanceViews.Status(future, StatusCases.Now).Header.AgeSeconds));
    }

    [Fact]
    public void A_desk_task_has_the_inputs_the_timeout_in_whole_seconds_on_its_state_and_the_budget_with_its_figures()
    {
        var status = InstanceViews.Status(Produce(), StatusCases.Now);

        Assert.Equal([new StatusInput("fact:spec", "spec.md", $"sha256:{EngineFixture.Fact("the spec").Sha256}")], status.Inputs);
        Assert.Equal(new StatusTimeout(TimeoutKind.Parks, 3 * 3600, "produce"), status.Timeout);
        Assert.Equal(new StatusBudget("state", 0, 5_000_000, new BudgetFigures(0, Limit: null)), status.Budget);
        Assert.Contains("  timeout   parks in 3h 0m (desk)\n".Replace("0m (desk)", "(desk)", StringComparison.Ordinal), status.Text, StringComparison.Ordinal);
        Assert.Contains("tokens 0 / 5M (state)", status.Text, StringComparison.Ordinal);
        Assert.Null(status.Answer);
        Assert.Equal("CompleteStep", status.Commands[0].ActionId.Value);
    }

    [Fact]
    public void A_budget_figure_in_the_text_is_a_token_count_in_k_or_M_units()
    {
        var parked = InstanceViews.Status(StatusCases.View("parked-budget"), StatusCases.Now);

        Assert.Contains("5M", parked.Text, StringComparison.Ordinal);
        Assert.Equal("1.5k", StatusText.Tokens(1500));
    }

    [Fact]
    public void Nothing_in_the_status_text_is_outside_printable_ASCII() =>
        Assert.All(StatusCases.Names, name => Assert.All(InstanceViews.Status(StatusCases.View(name), StatusCases.Now).Text, static c => Assert.True(c is '\n' or (>= ' ' and <= '~'), $"not plain ASCII: '{c}'")));

    [Fact]
    public void A_taken_over_instance_has_the_paused_line_and_no_answer()
    {
        var status = InstanceViews.Status(StatusCases.View("taken-over"), StatusCases.Now);

        Assert.Equal(new StatusPaused(40), status.Paused);
        Assert.Null(status.Answer);
        Assert.Equal([StatusVerb.Handback, StatusVerb.Cancel], status.Commands.Select(static c => c.Verb));
        Assert.Contains("timeout 40s left", status.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_park_mirroring_a_child_names_it()
    {
        var status = InstanceViews.Status(StatusCases.View("parked-no-progress-child"), StatusCases.Now);

        Assert.Equal(new StatusChild(WorkflowInstanceId.Of(InstanceAddress.From("T-3/step/1/step.4")), InstanceAddress.From("T-3/step/1/step.4"), "parked", "no-progress"), status.Child);
        Assert.Contains("T-3/step/1/step.4  parked: no-progress", status.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("parked-rounds", AnswerKind.Grant, "ApplyCommand:answer-grant", ActionClass.Protected, "sindri workflow answer T-3/step/1 --grant 1", true)]
    [InlineData("parked-no-progress-child", AnswerKind.Grant, "ApplyCommand:answer-grant", ActionClass.Protected, "sindri workflow answer T-3/step/1 --grant 1", true)]
    [InlineData("parked-budget", AnswerKind.GrantTokens, "ApplyCommand:answer-grant-tokens", ActionClass.Protected, "sindri workflow answer T-3/step/1 --grant-tokens 5M", true)]
    [InlineData("escalated-gate", AnswerKind.Accept, "ApplyCommand:answer-accept", ActionClass.Protected, "sindri workflow answer T-3/step/1 --accept", true)]
    [InlineData("parked-needs-answer-step", AnswerKind.Text, "ApplyCommand:answer-text", ActionClass.Desk, "sindri workflow answer T-3/step/1 --text <answer>", false)]
    [InlineData("parked-needs-answer-wait", AnswerKind.Resume, "ApplyCommand:answer-resume", ActionClass.Desk, "sindri workflow answer T-3/step/1 --resume [--text <answer>]", false)]
    [InlineData("parked-timeout", AnswerKind.Resume, "ApplyCommand:answer-resume", ActionClass.Desk, "sindri workflow answer T-3/step/1 --resume", false)]
    [InlineData("parked-no-runtime", AnswerKind.Resume, "ApplyCommand:answer-resume", ActionClass.Desk, "sindri workflow answer T-3/step/1 --resume", false)]
    [InlineData("parked-revoked", AnswerKind.CancelOnly, "ApplyCommand:cancel", ActionClass.Desk, "sindri workflow cancel T-3/step/1", false)]
    public void The_answer_line_is_the_kind_the_catalog_action_and_the_command_the_text_prints(string name, AnswerKind kind, string action, ActionClass actionClass, string cli, bool atTerminal)
    {
        var status = InstanceViews.Status(StatusCases.View(name), StatusCases.Now);

        Assert.Equal(new StatusAnswer(kind, Core.Ids.ActionId.From(action), actionClass, cli, atTerminal), status.Answer);
        Assert.Contains(cli, status.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_drift_park_has_no_answer_no_drift_line_and_the_protected_approve_command_after_cancel()
    {
        var status = InstanceViews.Status(StatusCases.View("parked-drift"), StatusCases.Now);

        Assert.Null(status.Answer);
        Assert.Null(status.Drift);   // the text has no 'drift' line either: the park's own next sentence names the block
        Assert.Equal([StatusVerb.Takeover, StatusVerb.Cancel, StatusVerb.Approve], status.Commands.Select(static c => c.Verb));
        Assert.Equal(
            new StatusCommand(StatusVerb.Approve, Core.Ids.ActionId.From("ApproveRecipe"), ActionClass.Protected, $"sindri recipe approve workflow.step@{StatusText.Hash12(EngineFixture.Step.Hash!.Value.Value)}", Confirm: null),
            status.Commands[^1]);
        Assert.Contains("step.produce changed since", status.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_drift_waiting_behind_another_park_is_the_note_beside_that_parks_own_answer()
    {
        var status = InstanceViews.Status(StatusCases.View("parked-rounds-with-drift"), StatusCases.Now);

        Assert.Equal(AnswerKind.Grant, status.Answer?.Kind);
        Assert.Equal(new StatusDrift(BlockId.From("step.produce"), NeedsReapproval: true), status.Drift);
    }

    [Fact]
    public void A_finished_instance_has_no_next_no_answer_and_no_commands()
    {
        var status = InstanceViews.Status(StatusCases.View("done"), StatusCases.Now);

        Assert.Equal((null, null, 0), (status.Next, status.Answer, status.Commands.Count));
    }

    [Fact]
    public void Every_command_row_is_a_row_of_the_catalog_with_the_same_class()
    {
        var catalog = Host.Catalog.EngineRows.All.ToDictionary(static r => r.Action.Value, static r => r.Class, StringComparer.Ordinal);

        foreach (var name in StatusCases.Names)
        {
            foreach (var row in InstanceViews.Status(StatusCases.View(name), StatusCases.Now).Commands)
            {
                Assert.True(catalog.TryGetValue(row.ActionId.Value, out var actionClass), $"{name}: {row.ActionId.Value} is no catalog row");
                Assert.Equal(actionClass, row.Class);
            }
        }
    }

    [Fact]
    public void A_workflow_that_has_not_built_its_instance_yet_shows_its_item_and_the_subject_it_has()
    {
        var subject = new SubjectRef("text", "fact:spec", "spec.md", Href: null);
        var view = InstanceViews.Starting(InstanceAddress.From("T-3/step/1"), StatusCases.View("desk-task").Recipe, StatusCases.Now, ItemKey.From("T-3"), subject);

        Assert.Equal((InstanceViews.StartingState, ItemKey.From("T-3"), subject), (view.State, view.ItemId, view.Subject));
    }

    [Fact]
    public void An_output_with_no_declared_type_that_is_produced_is_a_text_row_with_no_ref()
    {
        var recipe = _incident with { Recipe = _incident.Recipe with { Outputs = [new RecipeOutput(_output, "Output", Type: null, "produce")] } };
        var instance = EngineFixture.At(recipe.Recipe, "produce") with { Facts = new Dictionary<FactKey, FactValue> { [_spec] = EngineFixture.Fact("the spec"), [_output] = EngineFixture.Fact("done") } };

        var view = InstanceViews.Of(instance, recipe, StatusCases.Now, timeoutAt: null, new InstanceViewContext());

        var row = Assert.Single(view.Facts, static f => f.Direction == FactDirection.Output);
        Assert.Equal((FactType.Text, FactStatus.Produced), (row.Type, row.Status));
        Assert.Null(row.Ref);
    }

    private static IReadOnlyList<InputPresence> Slots(params RecipeInput[] inputs)
    {
        var recipe = StatusCases.Recipe with { Recipe = EngineFixture.Step with { Inputs = [.. inputs] }, Inputs = new Dictionary<FactKey, InputFact>() };
        return InputSlots.Of(EngineFixture.At(recipe.Recipe, "produce"), recipe);
    }

    [Fact]
    public void Every_input_has_its_kind_the_subject_adapter_as_the_recipe_view_serves_it()
    {
        var slots = Slots(
            new RecipeInput(FactKey.From("fact:project"), "Project", "payload", "linear-project", Generate: null),
            new RecipeInput(FactKey.From("fact:notes"), "Notes", "file", "file", Generate: null));

        Assert.Equal((string[])["linear-project", "file"], slots.Select(static s => s.Kind), StringComparer.Ordinal);
    }

    [Fact]
    public void A_slot_is_required_unless_its_input_generates_and_carries_its_declared_kind()
    {
        var slots = Slots(
            new RecipeInput(FactKey.From("fact:url"), "Url", "link", "link", Generate: null),
            new RecipeInput(FactKey.From("fact:note"), "Note", Type: null, "text", Generate: "note {start:date}"));

        Assert.Equal([("link", true), ("text", false)], slots.Select(static s => (s.Kind, s.Required)));
    }

    [Fact]
    public void A_recipe_registered_before_input_kinds_builds_a_view_that_names_the_fix_instead_of_throwing()
    {
        var legacy = _incident with { Recipe = _incident.Recipe with { Inputs = [new RecipeInput(_spec, "Spec", Type: null, Kind: null, Generate: null)] } };
        var instance = EngineFixture.At(legacy.Recipe, "produce");

        var view = InstanceViews.Of(instance, legacy, StatusCases.Now, timeoutAt: null, new InstanceViewContext());

        Assert.Equal("item", view.Subject?.Kind);   // never null on the wire: the list row's fallback, the item itself
        Assert.Equal(instance.Item.Value, view.Subject?.Key);
        Assert.Empty(view.Inputs);
        Assert.Contains("RCP-INPUT-001", view.Detail, StringComparison.Ordinal);
        Assert.Contains("register the current file at a new version and approve it", view.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_recipe_with_no_inputs_builds_a_view_too()
    {
        var legacy = _incident with { Recipe = _incident.Recipe with { Inputs = [] } };

        var view = InstanceViews.Of(EngineFixture.At(legacy.Recipe, "produce"), legacy, StatusCases.Now, timeoutAt: null, new InstanceViewContext());

        Assert.Contains("RCP-INPUT-002", view.Detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("link", "link")]
    [InlineData("item", "item")]
    [InlineData("file", "file")]
    [InlineData("webhook", "payload")]
    [InlineData("linear-project", "payload")]
    [InlineData("text", "text")]
    public void An_input_with_no_declared_type_has_the_type_its_kind_pairs_with_in_its_fact_row(string kind, string expected)
    {
        var recipe = _incident with { Recipe = _incident.Recipe with { Inputs = [new RecipeInput(_spec, "Spec", Type: null, kind, Generate: null)] } };

        var view = InstanceViews.Of(EngineFixture.At(recipe.Recipe, "produce"), recipe, StatusCases.Now, timeoutAt: null, new InstanceViewContext());

        Assert.Equal(FactTypes.Parse(expected), Assert.Single(view.Facts, static f => f.Key == "fact:spec").Type);
    }
}
