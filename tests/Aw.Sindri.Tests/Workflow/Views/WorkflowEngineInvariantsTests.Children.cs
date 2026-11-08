using System.Globalization;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Testing.Views;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Views;

/// <summary>Invariants 2 and 3: children sit under the current child wait, and their addresses carry the parent's entry.</summary>
public sealed partial class WorkflowEngineInvariantsTests
{
    [Fact, Trait("Lane", "unit")]
    public void Children_sit_under_the_current_child_wait_and_a_v1_wait_has_at_most_one_live_child() => Walk(Children);

    private static List<string> Children(View view, RecipeShape shape)
    {
        var state = StateOf(view, shape);
        var failures = new List<string>();
        if (CellOf(view).Children is not null && !state.Child)
        {
            failures.Add("child progress on a state that is not a child wait");
        }

        if (view.Detail is not { } detail)
        {
            return failures;
        }

        foreach (var group in detail.Children)
        {
            failures.AddRange(GroupFailures(view, shape, detail, group));
        }

        if (detail.Parent is { } parent)
        {
            failures.AddRange(ParentEntry(detail, parent));
        }

        return failures;
    }

    private static List<ChildRow> RowsOf(ChildGroup group) => [.. group.Workstreams.SelectMany(w => w.Rows), .. group.Rows];

    private static List<string> GroupFailures(View view, RecipeShape shape, InstanceDetail detail, ChildGroup group)
    {
        var state = StateOf(view, shape);
        var rows = RowsOf(group);
        var failures = new List<string>();
        if (!state.Child || group.State != view.State)
        {
            failures.Add(M($"children of '{group.State}' while the current state is '{view.State}'{(state.Child ? string.Empty : " (not a child wait)")}"));
        }

        if (group.Recipe.Value != state.ChildRecipe)
        {
            failures.Add(M($"children of {group.Recipe.Value}, the wait starts {state.ChildRecipe}"));
        }

        failures.AddRange(FanOut(view, detail, group, rows));
        if (CellOf(view).Children is { } progress && (progress.Done != group.Done || progress.Total != group.Total))
        {
            failures.Add(M($"the band says {progress.Done}/{progress.Total} children, the group {group.Done}/{group.Total}"));
        }

        failures.AddRange(ChildEntries(view, shape, group, rows));
        return failures;
    }

    private static List<string> FanOut(View view, InstanceDetail detail, ChildGroup group, List<ChildRow> rows)
    {
        var live = rows.Count(r => r.Glyph == StateGlyph.Active);
        return WorkflowViewFixtures.FanOutPreviews.Contains(view.Address) || (detail.Children.Count == 1 && group.Total == 1 && rows.Count <= 1 && live <= 1)
            ? []
            : [M($"{rows.Count} child rows (total {group.Total}, {live} live) under one v1 child wait; fan-out is schema v2 and must be marked (WF 3.9)")];
    }

    private static List<string> ChildEntries(View view, RecipeShape shape, ChildGroup group, List<ChildRow> rows)
    {
        var preview = WorkflowViewFixtures.FanOutPreviews.Contains(view.Address);
        var parent = InstanceAddress.From(view.Address);
        var looped = view.Cells.Any(c => c.Round is not null);
        return [.. rows.Select((row, i) => (row.Address.Value, Expected: preview ? i + 1 : shape.State(view.State).Order!.Value))
            .Where(r => r.Value != parent.Child(group.Recipe, r.Expected).Value && !(looped && !preview && Entry(r.Value) > r.Expected))
            .Select(r => M($"child address {r.Value}: the engine addresses it {parent.Child(group.Recipe, r.Expected).Value} (the parent's entry of '{view.State}'{(preview ? ", fan-out is sequential" : string.Empty)})"))];
    }

    private static int Entry(string address) => int.TryParse(address[(address.LastIndexOf('.') + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0;

    private static List<string> ParentEntry(InstanceDetail detail, ParentRef parent)
    {
        var parentId = "workflow." + parent.Address.Value.Split('/')[1];
        var childId = detail.Recipe.Id.Value;
        var (state, order) = ParentWait(parentId, childId);
        if (state is null)
        {
            return [M($"no declaration of the parent recipe {parentId} with a child wait on {childId}")];
        }

        var expected = parent.Address.Child(detail.Recipe.Id, order).Value;
        return detail.Address.Value == expected ? [] : [M($"the child of a {parentId} is created in '{state}' (entry {order}): {expected}, not {detail.Address.Value}")];
    }

    private static (string? State, int Order) ParentWait(string parentId, string childId)
    {
        if (FixtureRecipes.All.FirstOrDefault(s => s.Id == parentId)?.States.FirstOrDefault(s => s.ChildRecipe == childId) is { } wait)
        {
            return (wait.Id, wait.Order!.Value);
        }

        foreach (var path in ShippedRecipes.Recipes)
        {
            var recipe = RecipeComposition.ReadClean(ShippedRecipes.Read(path), path).Recipe;
            var found = recipe.Id.Value == parentId ? recipe.States.FirstOrDefault(s => s.Value.WaitFor?.Child?.Recipe.Value == childId).Key : null;
            if (found is not null)
            {
                return (found, StatePathOrder.Of(recipe).States.Single(p => p.State == found).Order!.Value);
            }
        }

        return (null, 0);
    }
}
