using System.Globalization;
using Aw.Sindri.Testing.Workflow;
using static Aw.Sindri.Tests.Workflow.Graph.GraphRecipe;

namespace Aw.Sindri.Tests.Workflow.Graph;

/// <summary>
/// RCP-CHILD-002 (child recipes form a cycle) and RCP-CHILD-003 (children nest more than 10 deep), spec §9.1. Children
/// resolve through the context's IChildRecipeResolver; a child it can't find is RCP-CHILD-001's (the wiring rules).
/// </summary>
public sealed class ChildRulesTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_recipe_that_starts_itself_is_a_cycle()
    {
        // The recipe of invalid/RCP-CHILD-002.yaml; the child's 'recipe' is at 8:25.
        var recipe = Parent("workflow.child-cycle", "workflow.child-cycle");

        Assert.Equal(
            ["RCP-CHILD-002: child recipes form a cycle: workflow.child-cycle -> workflow.child-cycle at RCP-CHILD-002.yaml:8:25 (states.running.waitFor.child.recipe); remove a child wait so no recipe starts itself, directly or through its children"],
            Lines(recipe, "RCP-CHILD-002.yaml", [("states.running.waitFor.child.recipe", 8, 25)]));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cycle_through_another_recipe_is_found_through_the_resolver()
    {
        var children = InMemoryChildRecipes.None
            .With(Child(Parent("workflow.b", "workflow.c")))
            .With(Child(Parent("workflow.c", "workflow.a")));

        Assert.Equal(
            ["RCP-CHILD-002: child recipes form a cycle: workflow.a -> workflow.b -> workflow.c -> workflow.a at r.yaml:1:1 (states.running.waitFor.child.recipe); remove a child wait so no recipe starts itself, directly or through its children"],
            Lines(Parent("workflow.a", "workflow.b"), children));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cycle_below_the_recipe_is_reported_once()
    {
        var children = InMemoryChildRecipes.None
            .With(Child(Parent("workflow.b", "workflow.c")))
            .With(Child(Parent("workflow.c", "workflow.b")));

        Assert.Equal(
            ["RCP-CHILD-002: child recipes form a cycle: workflow.a -> workflow.b -> workflow.c -> workflow.b at r.yaml:1:1 (states.running.waitFor.child.recipe); remove a child wait so no recipe starts itself, directly or through its children"],
            Lines(Parent("workflow.a", "workflow.b"), children));
    }

    [Fact, Trait("Lane", "unit")]
    public void Children_nested_more_than_ten_deep_are_refused()
    {
        // The recipe of invalid/RCP-CHILD-003.yaml (workflow.depth-0), with depth-1 to depth-11 given by the resolver.
        var children = Chain(11);

        Assert.Equal(
            ["RCP-CHILD-003: child recipes nest 11 deep (workflow.depth-0 -> workflow.depth-1 -> workflow.depth-2 -> workflow.depth-3 -> workflow.depth-4 -> workflow.depth-5 -> workflow.depth-6 -> workflow.depth-7 -> workflow.depth-8 -> workflow.depth-9 -> workflow.depth-10 -> workflow.depth-11), more than 10 at RCP-CHILD-003.yaml:8:25 (states.running.waitFor.child.recipe); flatten the chain so children nest at most 10 deep"],
            Lines(Parent("workflow.depth-0", "workflow.depth-1"), "RCP-CHILD-003.yaml", [("states.running.waitFor.child.recipe", 8, 25)], children));
    }

    [Fact, Trait("Lane", "unit")]
    public void Children_ten_deep_raise_nothing() =>
        Assert.Empty(Lines(Parent("workflow.depth-0", "workflow.depth-1"), Chain(10)));

    [Fact, Trait("Lane", "unit")]
    public void A_child_the_resolver_cannot_find_is_left_to_the_wiring_rules() =>
        Assert.Empty(Lines(Parent("workflow.item", "workflow.step")));

    [Fact, Trait("Lane", "unit")]
    public void The_item_and_step_recipes_raise_nothing()
    {
        // workflow.item waits for workflow.step, which has no children (spec §12).
        var step = Of("produce", [("produce", Active()), ("done", Terminal())], [Go("produce", "done")], id: "workflow.step");

        Assert.Empty(Lines(Parent("workflow.item", "workflow.step"), InMemoryChildRecipes.None.With(Child(step))));
    }

    /// <summary>workflow.depth-1 to workflow.depth-n, each waiting on the next; the last waits on nothing.</summary>
    private static InMemoryChildRecipes Chain(int n)
    {
        var children = InMemoryChildRecipes.None;
        for (var i = 1; i < n; i++)
        {
            _ = children.With(Child(Parent(Depth(i), Depth(i + 1))));
        }

        return children.With(Child(Of("produce", [("produce", Active()), ("done", Terminal())], [Go("produce", "done")], id: Depth(n))));
    }

    private static string Depth(int level) => "workflow.depth-" + level.ToString(CultureInfo.InvariantCulture);
}
