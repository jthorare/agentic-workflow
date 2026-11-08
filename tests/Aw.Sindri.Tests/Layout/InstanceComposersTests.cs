using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.GetInstance;
using RecipeUrgency = Aw.Sindri.Core.Workflow.Recipes.RecipeUrgency;

namespace Aw.Sindri.Tests.Layout;

/// <summary>InstanceViews.Detail and InstanceSummaries.Of are pure functions of (view, context): a hand-built view in, the API's records out, no clock and no I/O.</summary>
[Lane(TestLane.Unit)]
public sealed class InstanceComposersTests
{
    private static readonly JsonSerializerOptions _options = AwJson.CreateOptions(SindriJsonContext.Default);
    private static readonly InstanceAddress _address = InstanceAddress.From("ITM-9/step/1");
    private static readonly DateTimeOffset _since = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _asOf = new(2026, 10, 12, 9, 30, 0, TimeSpan.Zero);

    private static InstanceView Built()
    {
        var recipe = new RecipeVersion(RecipeId.From("workflow.step"), 3, RecipeHash.From("sha256:" + new string('a', 64)));
        return new InstanceView(_address, recipe, "produce", Detail: null, _since, "wait", [new AllowedCommand("ApplyCommand:cancel", "sindri workflow cancel ITM-9/step/1")], Question: null, Task: null, Run: null, Children: [], Outcome: null)
        {
            Id = WorkflowInstanceId.Of(_address),
            ItemId = ItemKey.From("ITM-9"),
            Urgency = RecipeUrgency.Triage,
            Step = new StepDeclaration("active", "step.produce", "step.produce"),
        };
    }

    [Fact]
    public void Detail_takes_title_parent_counts_and_as_of_from_its_context_and_the_rest_from_the_view()
    {
        var view = Built();
        var parent = new ParentRef(WorkflowInstanceId.Of(InstanceAddress.From("ITM-1/item/1")), InstanceAddress.From("ITM-1/item/1"), "Parent", "build", Workstream: null);
        var counts = new ViewCounts(7, 1, 2, Context: null);

        var detail = InstanceViews.Detail(view, new InstanceDetailContext("A title", parent, counts, _asOf));

        Assert.Equal(("A title", parent, counts, _asOf), (detail.Title, detail.Parent, detail.Counts, detail.AsOf));
        Assert.Equal((view.Address, view.Id, view.ItemId, view.State, view.Since), (detail.Address, detail.Id, detail.ItemId, detail.State, detail.Since));
        Assert.Equal(new InstanceRecipe(view.Recipe.Id, view.Recipe.Hash, 3, RecipeUrgency.Triage, DisplayName: null), detail.Recipe);
        Assert.Equal(JsonSerializer.Serialize(new CurrentState("produce", view.Step, [], Desk: null, InstanceViews.Status(view, _asOf)), _options), JsonSerializer.Serialize(detail.Current, _options));
        Assert.Equal([ActionId.From("ApplyCommand:cancel")], detail.Actions);
        Assert.Equal("item", detail.Subject.Kind);   // a recipe with no input is subject-less: the item stands in
    }

    [Fact]
    public void Detail_is_the_same_for_the_same_view_and_context()
    {
        var context = new InstanceDetailContext("t", Parent: null, new ViewCounts(0, 0, 0, Context: null), _asOf);

        Assert.Equal(JsonSerializer.Serialize(InstanceViews.Detail(Built(), context), _options), JsonSerializer.Serialize(InstanceViews.Detail(Built(), context), _options));
    }

    [Fact]
    public void A_summary_has_the_id_the_chip_the_answer_and_the_action_ids_of_the_view()
    {
        var view = Built();

        var summary = InstanceSummaries.Of(view, new InstanceSummaryContext(_asOf));

        Assert.Equal((view.Id, new StateChip("RUNNING", ChipTone.Ok)), (summary.Id, summary.Chip));
        Assert.Equal([ActionId.From("ApplyCommand:cancel")], summary.Actions);
        Assert.Null(summary.Answer);
        Assert.Equal(new StateChip("QUEUED", ChipTone.Neutral), InstanceSummaries.Of(view, new InstanceSummaryContext(_asOf, NotStarted: true)).Chip);
    }

    [Fact]
    public void A_view_that_is_still_starting_is_a_queued_row_and_a_detail_not_an_error()
    {
        var starting = new InstanceView(_address, Built().Recipe, InstanceViews.StartingState, Detail: null, _since, string.Empty, [], Question: null, Task: null, Run: null, Children: [], Outcome: null)
        {
            Id = WorkflowInstanceId.Of(_address),
        };

        var summary = InstanceSummaries.Of(starting, new InstanceSummaryContext(_asOf));
        var detail = InstanceViews.Detail(starting, new InstanceDetailContext("t", Parent: null, new ViewCounts(0, 0, 0, Context: null), _asOf));

        Assert.Equal((new StateChip("QUEUED", ChipTone.Neutral), "queued", InstanceViews.StartingState), (summary.Chip, summary.Now, summary.State));
        Assert.Empty(summary.States);
        Assert.Empty(summary.Actions);
        Assert.Equal(InstanceViews.StartingState, detail.Current.Status.Header.State);
    }

    [Fact]
    public void A_budget_with_no_cap_prints_used_and_no_limit_and_serves_a_null_limit()
    {
        var view = StatusCases.View("desk-task") with { Budget = new BudgetView(84_000, StateCap: null, 120_000, InstanceCap: null) };

        var text = StatusLayout.Render(view, _asOf);

        Assert.Contains("tokens 84k used (state), 120k used (instance)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("84k /", text, StringComparison.Ordinal);
        var line = new StatusBudget("state", 84_000, Limit: null, new BudgetFigures(120_000, Limit: null));
        Assert.Null(line.Limit);
    }
}
