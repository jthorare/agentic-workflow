using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The name a screen reader hears for a stage cell uses the recipe's words for the state, as the rows do, and its id made readable when the recipe gives none.</summary>
[Lane(TestLane.Unit)]
public sealed class StageCellNameTests
{
    private static InstanceSummary Served => FixtureInstances.List("open").Instances.First(i => i.States.Count > 1);

    private static StateNames Names(InstanceSummary row, string stateName)
    {
        var names = new StateNames();
        _ = names.Add(FixtureInstances.Recipe("workflow.project") with
        {
            Id = row.Recipe.Id,
            States = [.. row.States.Select(s => new RecipeStateView(s.State, Order: 1, BranchOf: null, Why: null, string.Equals(s.State, row.States[0].State, StringComparison.Ordinal) ? stateName : null))],
        });
        return names;
    }

    [Fact]
    public void A_cell_is_named_by_the_recipes_words_for_its_state()
    {
        var cell = Served.States[0];

        var named = new StageCellViewModel(cell, isCurrent: false, isBlocked: false, Names(Served, "Design the change"), Served.Recipe.Id);

        Assert.StartsWith("Design the change, ", named.Name, StringComparison.Ordinal);
        Assert.Equal(cell.State, named.State);   // the id stays the cell's State
    }

    [Fact]
    public void A_state_the_recipe_does_not_name_reads_as_its_id_made_readable()
    {
        var cell = Served.States[1];

        var named = new StageCellViewModel(cell, isCurrent: false, isBlocked: false, Names(Served, "Design the change"), Served.Recipe.Id);

        Assert.StartsWith(WorkflowNames.Of(cell.State, displayName: null) + ", ", named.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Without_a_lookup_the_cell_keeps_the_state_id()
    {
        var cell = Served.States[0];

        Assert.StartsWith(cell.State + ", ", new StageCellViewModel(cell, isCurrent: false, isBlocked: false).Name, StringComparison.Ordinal);
    }

    [Fact]
    public void A_rows_cells_are_named_through_the_rows_lookup()
    {
        var row = new InstanceRowViewModel(Served, new FakeClipboard(), new FakeTimeProvider(A.Noon), Names(Served, "Design the change"));

        Assert.StartsWith("Design the change, ", row.Stages[0].Name, StringComparison.Ordinal);
        Assert.All(row.Stages.Skip(1), c => Assert.StartsWith(WorkflowNames.Of(c.State, displayName: null) + ", ", c.Name, StringComparison.Ordinal));
    }
}
