using Aw.Sindri.Core.Workflow.Recipes;
using static Aw.Sindri.Tests.Workflow.Graph.GraphRecipe;

namespace Aw.Sindri.Tests.Workflow.Graph;

/// <summary>RCP-NEXT-001 (only recipe variables, and loops that exist) and RCP-NEXT-002 (no approval-tier verbs), spec §3.7.</summary>
public sealed class NextRulesTests
{
    private static readonly (string, LoopSpec)[] _revisions = [("revisions", new LoopSpec(3, LoopSpec.DefaultNoProgress))];

    [Fact, Trait("Lane", "unit")]
    public void An_unknown_variable_is_refused()
    {
        // The recipe of invalid/RCP-NEXT-001.yaml; 'next' is at 10:5.
        var recipe = Of(
            "produce",
            [("produce", Active(next: "Produce the Step output for {ticket}.")), ("done", Terminal())],
            [Go("produce", "done")],
            id: "workflow.next-variable");

        Assert.Equal(
            ["RCP-NEXT-001: the next of state 'produce' uses '{ticket}', which isn't a recipe variable at RCP-NEXT-001.yaml:10:5 (states.produce.next); use only {item}, {instance}, {state}, {user}, {child}, {loop.<name>}, {loop.<name>.cap} and {budget.tokens.left}"],
            Lines(recipe, "RCP-NEXT-001.yaml", [("states.produce.next", 10, 5)]));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("{reason}")]
    [InlineData("{question}")]
    [InlineData("{loop}")]
    [InlineData("{loop.revisions.left}")]
    [InlineData("{budget.tokens}")]
    [InlineData("{}")]
    public void Engine_only_and_malformed_variables_are_refused(string variable)
    {
        var recipe = Of("produce", [("produce", Active(next: $"Produce it {variable}.")), ("done", Terminal())], [Go("produce", "done")], _revisions);

        Assert.Equal(
            [$"RCP-NEXT-001: the next of state 'produce' uses '{variable}', which isn't a recipe variable at r.yaml:1:1 (states.produce.next); use only {{item}}, {{instance}}, {{state}}, {{user}}, {{child}}, {{loop.<name>}}, {{loop.<name>.cap}} and {{budget.tokens.left}}"],
            Lines(recipe));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("{loop.revision}")]
    [InlineData("{loop.revision.cap}")]
    public void A_loop_variable_must_name_a_declared_loop(string variable)
    {
        var recipe = Of("produce", [("produce", Active(next: $"Round {variable}.")), ("done", Terminal())], [Go("produce", "done")], _revisions);

        Assert.Equal(
            [$"RCP-NEXT-001: the next of state 'produce' uses '{variable}', but loop 'revision' isn't declared at r.yaml:1:1 (states.produce.next); declare it under loops, or name a declared loop"],
            Lines(recipe));
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_unknown_variable_is_reported()
    {
        var recipe = Of("produce", [("produce", Active(next: "{a} and {b}")), ("done", Terminal())], [Go("produce", "done")]);

        Assert.Equal(2, Lines(recipe).Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_approval_tier_verb_is_refused()
    {
        // The recipe of invalid/RCP-NEXT-002.yaml; 'next' is at 10:5.
        var recipe = Of(
            "produce",
            [("produce", Active(next: "Produce the Step output, then run sindri workflow answer {instance} --grant 3.")), ("done", Terminal())],
            [Go("produce", "done")],
            id: "workflow.next-approval");

        Assert.Equal(
            ["RCP-NEXT-002: the next of state 'produce' tells the reader to run '--grant', an approval-tier command at RCP-NEXT-002.yaml:10:5 (states.produce.next); leave it out, since only a human at a terminal runs approval-tier commands"],
            Lines(recipe, "RCP-NEXT-002.yaml", [("states.produce.next", 10, 5)]));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("Then run sindri recipe approve workflow.step.", "recipe approve")]
    [InlineData("Run `sindri recipe revoke {item}`.", "recipe revoke")]
    [InlineData("Run sindri profile approve.", "profile approve")]
    [InlineData("Answer with --grant-tokens 1000.", "--grant-tokens")]
    [InlineData("Answer with --accept.", "--accept")]
    [InlineData("Answer with --GRANT 2.", "--GRANT")]
    public void Every_approval_tier_command_is_refused(string next, string command)
    {
        var recipe = Of("produce", [("produce", Active(next: next)), ("done", Terminal())], [Go("produce", "done")]);

        _ = Assert.Single(Lines(recipe), line => line.StartsWith($"RCP-NEXT-002: the next of state 'produce' tells the reader to run '{command}'", StringComparison.Ordinal));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("Waiting for the router to size and route {item}.")]
    [InlineData("Produce the Step output for {item} (round {loop.revisions}/{loop.revisions.cap}).")]
    [InlineData("{user}: {instance} is in {state}; {child} has {budget.tokens.left} tokens left.")]
    [InlineData("Review the output as the verifier. Accept it or reject it.")]
    public void A_next_with_recipe_variables_only_raises_nothing(string next)
    {
        var recipe = Of("produce", [("produce", Active(next: next)), ("done", Terminal())], [Go("produce", "done")], _revisions);

        Assert.Empty(Lines(recipe));
    }
}
