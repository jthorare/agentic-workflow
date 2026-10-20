using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>The cap each looped state gives its Step runs, from the recipe's loop membership, whichever state the instance is in.</summary>
[Lane(TestLane.Unit)]
public sealed class StepRunCapsTests
{
    private static readonly Recipe _loop = LoopOver("produce", "checks", "verify");

    [Fact]
    public void Every_member_of_a_loop_has_the_recipes_cap_including_one_at_neither_end_of_the_back_edge_when_the_instance_is_elsewhere()
    {
        var caps = StepRunCaps.Of(_loop, [Cell("produce", round: null), Cell("checks", round: null), Cell("verify", round: null), Cell("done", round: null)]);

        Assert.Equal([("checks", 3), ("produce", 3), ("verify", 3)], caps.OrderBy(static c => c.Key, StringComparer.Ordinal).Select(static c => (c.Key, c.Value)));
    }

    [Fact]
    public void A_state_in_no_loop_has_no_cap()
    {
        var caps = StepRunCaps.Of(_loop, []);

        Assert.False(caps.ContainsKey("done"));
    }

    [Fact]
    public void A_granted_round_on_the_active_member_raises_the_cap_of_every_member_of_its_loop()
    {
        var caps = StepRunCaps.Of(_loop, [Cell("produce", round: null), Cell("checks", new Round(3, 4)), Cell("verify", round: null)]);

        Assert.Equal([4, 4, 4], [caps["produce"], caps["checks"], caps["verify"]]);
    }

    private static Recipe LoopOver(params string[] members)
    {
        var step = EngineFixture.Step;
        var states = step.States.ToDictionary(static s => s.Key, static s => s.Value, StringComparer.Ordinal);
        foreach (var member in members)
        {
            states[member] = states[member] with { Loop = "revisions" };
        }

        return step with { States = states, Loops = new Dictionary<string, LoopSpec>(StringComparer.Ordinal) { ["revisions"] = new(3, LoopSpec.DefaultNoProgress) } };
    }

    private static StateCell Cell(string state, Round? round) =>
        new(state, Order: 1, BranchOf: null, StateGlyph.Pending, Badge: null, round, [], Desk: false, Child: false, Children: null, Outcome: null, Terminal: false);
}
