using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Testing.Views;

namespace Aw.Sindri.Tests.Workflow.Views;

/// <summary>
/// The goldens are one scenario seen from many routes (screens spec fixture.md): an instance, its fact, its children and its
/// recipe must say the same things in every file. These tests pin that agreement across the fixtures.
/// </summary>
public sealed class WorkflowViewConsistencyTests
{
    private static IReadOnlyList<InstanceSummary> Roots => WorkflowViewFixtures.RootList().Instances;

    private static InstanceDetail Project => WorkflowViewFixtures.ProjectBuild();

    private static string Name(EngineOwnedKind kind) => kind switch
    {
        EngineOwnedKind.Parked => "parked",
        EngineOwnedKind.Escalated => "escalated",
        EngineOwnedKind.TakenOver => "taken-over",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "no such kind"),
    };

    private static RecipeView RecipeOf(string id) => id switch
    {
        "workflow.project" => WorkflowViewFixtures.ProjectRecipe(),
        "workflow.incident-response" => WorkflowViewFixtures.IncidentRecipe(),
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "no recipe golden"),
    };

    [Fact, Trait("Lane", "unit")]
    public void The_header_state_is_the_engine_word_when_the_engine_owns_the_instance_else_the_recipe_state()
    {
        foreach (var view in WorkflowViewFixtures.AllDetails())
        {
            var header = view.Current.Status.Header;
            Assert.Equal(view.EngineOwned is { } owned ? Name(owned.Kind) : view.State, header.State);
        }

        foreach (var status in WorkflowViewFixtures.AllStatuses())
        {
            Assert.DoesNotContain(':', status.Header.Detail ?? string.Empty);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void A_taken_over_instance_has_one_shape_everywhere_and_no_answer()
    {
        foreach (var row in Roots.Where(r => r.EngineOwned is { Kind: EngineOwnedKind.TakenOver }))
        {
            Assert.Equal("WITH YOU", row.Chip.Word);
            Assert.Null(row.Answer);
            Assert.DoesNotContain(row.Actions, a => a.Value.StartsWith("ApplyCommand:answer", StringComparison.Ordinal));
            Assert.Contains(row.Actions, a => a.Value == "ApplyCommand:handback");
        }

        foreach (var status in WorkflowViewFixtures.AllStatuses().Where(s => s.Answer is not null))
        {
            var root = Roots.SingleOrDefault(r => r.Address.Value == status.Header.Address.Value);
            Assert.True(root is null || root.EngineOwned?.Kind != EngineOwnedKind.TakenOver, status.Header.Address.Value);
        }

        var takenOver = WorkflowViewFixtures.StatusTakenOver();
        Assert.Equal("taken-over", takenOver.Header.State);
        Assert.Equal(["handback", "cancel"], takenOver.Commands.Select(c => StatusVerbNames.Of(c.Verb)), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_list_row_and_its_detail_agree()
    {
        foreach (var view in WorkflowViewFixtures.AllDetails())
        {
            var row = Roots.SingleOrDefault(r => r.Address.Value == view.Address.Value);
            if (row is null)
            {
                continue;
            }

            Assert.Equal(row.State, view.State);
            Assert.Equal(row.Id, view.Id);
            Assert.Equal(row.EngineOwned, view.EngineOwned);
            Assert.Equal(row.Actions, view.Actions);
            Assert.Equal(row.States, view.States);
            Assert.Equal(row.Answer, view.Current.Status.Answer);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void A_fact_body_agrees_with_its_row()
    {
        var rows = Project.Facts.ToDictionary(f => f.Key, StringComparer.Ordinal);
        foreach (var (key, scenario) in new[] { ("fact:scope", "scope-map"), ("fact:packet", "packet"), ("fact:evidence", "evidence") })
        {
            var view = WorkflowViewFixtures.Fact(scenario);
            Assert.Equal(key, view.Key);
            Assert.Equal(rows[key].Type, view.Type);
        }

        var packet = WorkflowViewFixtures.Fact("packet").Body;
        var tasks = packet.GetProperty("workstreams").EnumerateArray().Sum(w => w.GetProperty("tasks").GetArrayLength());
        Assert.True(packet.GetProperty("frozen").GetBoolean());
        Assert.Equal($"{tasks.ToString(CultureInfo.InvariantCulture)} tasks in {packet.GetProperty("workstreams").GetArrayLength().ToString(CultureInfo.InvariantCulture)} workstreams, frozen", rows["fact:packet"].Summary);
        Assert.All(packet.GetProperty("checks").EnumerateArray(), c => Assert.Equal("pass", c.GetProperty("result").GetString()));

        var scope = WorkflowViewFixtures.Fact("scope-map").Body.GetProperty("surfaces").EnumerateArray().ToList();
        var files = scope.Sum(s => s.GetProperty("files").GetArrayLength());
        Assert.Equal($"{scope.Count.ToString(CultureInfo.InvariantCulture)} areas, {files.ToString(CultureInfo.InvariantCulture)} cited files", rows["fact:scope"].Summary);

        var evidence = WorkflowViewFixtures.Fact("evidence").Body;
        Assert.Equal(rows["fact:evidence"].Progress!.Done, evidence.GetProperty("done").GetInt32());
        Assert.Equal(rows["fact:evidence"].Progress!.Total, evidence.GetProperty("total").GetInt32());
        Assert.Equal(evidence.GetProperty("done").GetInt32(), evidence.GetProperty("packs").GetArrayLength());
    }

    [Fact, Trait("Lane", "unit")]
    public void The_packet_maps_its_tasks_to_the_children_of_the_screens_fixture()
    {
        var packet = WorkflowViewFixtures.Fact("packet").Body;
        var items = packet.GetProperty("workstreams").EnumerateArray()
            .ToDictionary(w => w.GetProperty("id").GetString()!, w => w.GetProperty("tasks").EnumerateArray().Select(t => t.GetProperty("item").GetString()!).Order(StringComparer.Ordinal).ToList(), StringComparer.Ordinal);
        foreach (var stream in Project.Children.Single().Workstreams)
        {
            Assert.Equal(stream.Rows.Select(r => r.ItemId!.Value.Value).Order(StringComparer.Ordinal), items[stream.Stream], StringComparer.Ordinal);
        }

        string? task(string id) => packet.GetProperty("workstreams").EnumerateArray().SelectMany(w => w.GetProperty("tasks").EnumerateArray()).Single(t => t.GetProperty("id").GetString() == id).GetProperty("item").GetString();
        Assert.Equal("ITM-01JD6", task("t4"));
        Assert.Equal("ITM-01JF3", task("t6"));
        Assert.Equal("ITM-01JF4", task("t7"));
        Assert.Equal("ITM-01JF8", task("t8"));
        Assert.Equal(9, items.Values.Sum(v => v.Count));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unfrozen_packet_carries_the_failing_check()
    {
        var packet = WorkflowViewFixtures.Fact("packet-unfrozen").Body;
        Assert.False(packet.GetProperty("frozen").GetBoolean());
        Assert.Contains(packet.GetProperty("checks").EnumerateArray(), c => c.GetProperty("id").GetString() == "PKT-SCOPE-002" && c.GetProperty("result").GetString() == "fail");
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("scope-map")]
    [InlineData("packet")]
    [InlineData("packet-unfrozen")]
    [InlineData("evidence")]
    [InlineData("text")]
    [InlineData("payload")]
    [InlineData("truncated")]
    public void Every_fact_body_is_reachable_from_a_fact_row(string scenario)
    {
        var view = WorkflowViewFixtures.Fact(scenario);
        var rows = WorkflowViewFixtures.AllDetails().SelectMany(d => d.Facts);
        Assert.Contains(rows, r => r.Ref?.FactKey == view.Key && r.Type == view.Type);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_finished_instance_has_no_next_line_and_no_empty_string()
    {
        Assert.Null(WorkflowViewFixtures.StatusDone().Next);
        Assert.Null(WorkflowViewFixtures.Done().Current.Status.Next);
        Assert.All(WorkflowViewFixtures.AllStatuses().Where(s => s.Next is not null), s => Assert.NotEmpty(s.Next!));
    }

    [Fact, Trait("Lane", "unit")]
    public void Now_is_a_short_sentence()
    {
        Assert.All(Roots, r => Assert.InRange(r.Now.Length, 1, 80));
        Assert.Equal("agent rolling back in ITM-01JG2", Roots.Single(r => r.Address.Value == "ITM-01JK1/incident-response/1").Now);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("workflow.project")]
    [InlineData("workflow.incident-response")]
    public void Loops_are_per_recipe_and_every_instance_of_the_recipe_carries_them(string recipeId)
    {
        var expected = RecipeOf(recipeId).Loops.SelectMany(l => new[] { (l.From, l.To), (l.To, l.From) }).Distinct().Order().ToList();
        var cells = Roots.Where(r => r.Recipe.Id.Value == recipeId).Select(r => r.States)
            .Concat(WorkflowViewFixtures.AllDetails().Where(d => d.Recipe.Id.Value == recipeId).Select(d => d.States));
        foreach (var states in cells)
        {
            var actual = states.SelectMany(c => c.Loops.Select(l => (c.State, l.With))).Distinct().Order().ToList();
            Assert.Equal(expected, actual);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void Children_are_addressed_under_their_parent_and_offer_what_their_state_allows()
    {
        foreach (var view in WorkflowViewFixtures.AllDetails())
        {
            foreach (var group in view.Children)
            {
                Assert.StartsWith("workflow.", group.Recipe.Value, StringComparison.Ordinal);
                foreach (var row in group.Rows.Concat(group.Workstreams.SelectMany(w => w.Rows)))
                {
                    Assert.StartsWith(view.Address.Value + "/", row.Address.Value, StringComparison.Ordinal);
                    if (row.Glyph == StateGlyph.Done)
                    {
                        Assert.DoesNotContain(row.Actions, a => a.Value.StartsWith("ApplyCommand:", StringComparison.Ordinal));
                    }

                    if (row.EngineOwned?.Kind == EngineOwnedKind.Parked)
                    {
                        Assert.Contains(row.Actions, a => a.Value.StartsWith("ApplyCommand:answer", StringComparison.Ordinal));
                    }

                    // A child's park is shown on the parent's band; it doesn't park the parent (Q-API-5).
                    Assert.Equal(row.EngineOwned?.Kind == EngineOwnedKind.Parked ? row.EngineOwned : null, row.MirroredPark);
                }
            }
        }

        var rows = Project.Children.Single().Workstreams.SelectMany(w => w.Rows).ToDictionary(r => r.ItemId!.Value.Value, StringComparer.Ordinal);
        Assert.Null(rows["ITM-01JD6"].Agent);
        Assert.Equal(0, rows["ITM-01JD6"].AgeSeconds);
        Assert.Equal([3120L, 2400L, 720L], [rows["ITM-01JF3"].AgeSeconds, rows["ITM-01JF4"].AgeSeconds, rows["ITM-01JF8"].AgeSeconds]);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_open_child_makes_its_state_active_and_open_item_needs_an_item()
    {
        var incident = WorkflowViewFixtures.IncidentLoop();
        _ = Assert.Single(incident.States, s => s.Glyph == StateGlyph.Active);
        var parentWait = WorkflowViewFixtures.ProjectBuild();
        Assert.Equal(StateGlyph.Active, parentWait.States.Single(s => s.State == "build").Glyph);
        foreach (var row in Roots)
        {
            Assert.True(row.ItemId is not null || row.Actions.All(a => a.Value != "OpenItem"), row.Address.Value);
        }

        foreach (var view in WorkflowViewFixtures.AllDetails())
        {
            Assert.True(view.ItemId is not null || view.Actions.All(a => a.Value != "OpenItem"), view.Address.Value);
        }
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("workflow.project")]
    [InlineData("workflow.incident-response")]
    public void A_fact_row_and_an_input_slot_equal_the_recipe_declaration(string recipeId)
    {
        var recipe = RecipeOf(recipeId);
        foreach (var view in WorkflowViewFixtures.AllDetails().Where(d => d.Recipe.Id.Value == recipeId))
        {
            foreach (var row in view.Facts)
            {
                var declared = recipe.Inputs.Where(i => i.Key == row.Key).Select(i => (i.Label, $"{i.Type}"))
                    .Concat(recipe.Outputs.Where(o => o.Key == row.Key).Select(o => (o.Label, $"{o.Type}"))).Single();
                Assert.Equal(declared.Label, row.Label);
                Assert.Equal(declared.Item2, $"{row.Type}");
            }
        }

        foreach (var row in Roots.Where(r => r.Recipe.Id.Value == recipeId))
        {
            Assert.Equal(recipe.Inputs.Select(i => (i.Key, i.Label, i.Kind)), row.Inputs.Select(i => (i.Key, (string?)i.Label, i.Kind)));
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void The_actor_of_a_fact_is_a_prefixed_string()
    {
        var actor = new Regex("^(human:[a-z-]+|step\\.[a-z.-]+|agent:[a-z-]+|service:[a-z-]+)$", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
        foreach (var by in WorkflowViewFixtures.AllDetails().SelectMany(d => d.Facts).Select(f => f.By).Where(b => b is not null))
        {
            Assert.Matches(actor, by);
        }

        Assert.DoesNotContain(WorkflowViewFixtures.AllDetails().SelectMany(d => d.Facts), f => f.By == "human:webhook");
    }

    [Fact, Trait("Lane", "unit")]
    public void The_home_strip_is_the_first_five_rows_of_the_list()
    {
        Assert.Equal(
            ["ITM-01JK1/incident-response/1", "ITM-01JK2/triage-alert/1", "ITM-01JK3/project/1", "ITM-01JK4/project/1", "ITM-01JK5/project/1"],
            Roots.Take(5).Select(r => r.Address.Value),
            StringComparer.Ordinal);
        Assert.Equal(12, Roots.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_budget_park_does_not_reuse_an_item_of_the_screens_fixture() =>
        Assert.DoesNotContain("ITM-01JC8", WorkflowViewFixtures.StatusParkedBudget().Header.Address.Value, StringComparison.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void Instance_ids_carry_the_uuid_v8_version_and_variant_bits_of_the_engine()
    {
        Assert.All(Roots, r =>
        {
            var hex = r.Id.Value["WFI-".Length..];
            Assert.Equal('8', hex[12]);
            Assert.Contains(hex[16], "89ab");
        });
    }

    [Fact, Trait("Lane", "unit")]
    public void The_status_text_follows_the_engine_wording_for_the_clock_tokens_and_drift()
    {
        var drift = Regex.Replace(WorkflowViewFixtures.StatusParkedDrift().Text, "\\s+", " ", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
        Assert.Contains("changed since workflow.project@", drift, StringComparison.Ordinal);
        Assert.Contains("was approved answer none (drift): sindri recipe approve workflow.project@", drift, StringComparison.Ordinal);

        var waiting = WorkflowViewFixtures.StatusNeedsAnswerWait();
        Assert.Matches(new Regex("since \\d\\d:\\d\\d \\(", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1)), waiting.Text);

        var fields = WorkflowViewFixtures.StatusDeskTask() with { Budget = new StatusBudget("state", 1_500, 200_000, new BudgetFigures(0, 400_000)) };
        Assert.Contains("tokens 1.5k / 200k (state)", StatusText.Render(fields, WorkflowViewFixtures.Now), StringComparison.Ordinal);

        var aged = fields with { Header = fields.Header with { Since = WorkflowViewFixtures.Now.AddHours(-25), AgeSeconds = 25 * 3600 } };
        Assert.Matches(new Regex("since 10-11 \\d\\d:\\d\\d \\(", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1)), StatusText.Render(aged, WorkflowViewFixtures.Now));
        var short1 = fields with { Header = fields.Header with { Since = WorkflowViewFixtures.Now.AddHours(-20), AgeSeconds = 20 * 3600 } };
        Assert.DoesNotContain("since 10-", StatusText.Render(short1, WorkflowViewFixtures.Now), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cancel_sentence_rejects_negative_counts()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => ConfirmText.Cancel(InstanceAddress.From("ITM-01JK1/item/1"), -1, 0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => ConfirmText.Cancel(InstanceAddress.From("ITM-01JK1/item/1"), 0, -1));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_needs_answer_park_is_its_own_instance_not_the_taken_over_one()
    {
        var park = WorkflowViewFixtures.StatusNeedsAnswerWait();
        Assert.NotEqual("ITM-01JK4/project/1", park.Header.Address.Value, StringComparer.Ordinal);
        Assert.Equal("parked", park.Header.State);
        Assert.Equal("needs-answer", park.Header.Detail);
        Assert.Equal(JsonValueKind.Object, WorkflowViewFixtures.Fact("packet").Body.ValueKind);
    }
}
