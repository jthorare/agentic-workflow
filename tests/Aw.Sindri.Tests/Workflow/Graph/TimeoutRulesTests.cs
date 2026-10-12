using Aw.Sindri.Core.Workflow.Recipes;
using static Aw.Sindri.Tests.Workflow.Graph.GraphRecipe;

namespace Aw.Sindri.Tests.Workflow.Graph;

/// <summary>RCP-TIME-001 (every active and wait state has a timeout) and RCP-TIME-002 (no retry on a child wait), spec §3.4.</summary>
public sealed class TimeoutRulesTests
{
    [Fact, Trait("Lane", "unit")]
    public void An_active_state_without_a_timeout_is_reported_where_the_state_starts()
    {
        // The recipe of invalid/RCP-TIME-001.yaml; 'produce' starts at 6:3 and has no timeout node.
        var recipe = Of("produce", [("produce", Active(then: null)), ("done", Terminal())], [Go("produce", "done")], id: "workflow.time");

        Assert.Equal(
            ["RCP-TIME-001: active state 'produce' has no timeout at RCP-TIME-001.yaml:6:3 (states.produce.timeout); add timeout: { after, then: park | escalate | retry }"],
            Lines(recipe, "RCP-TIME-001.yaml", [("states.produce", 6, 3)]));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_wait_state_without_a_timeout_is_reported()
    {
        var recipe = Of("triaged", [("triaged", WaitForEvent("item.routed", then: null)), ("done", Terminal())], [Go("triaged", "done", "wait.satisfied")]);

        Assert.Equal(
            ["RCP-TIME-001: wait state 'triaged' has no timeout at r.yaml:1:1 (states.triaged.timeout); add timeout: { after, then: park | escalate | retry }"],
            Lines(recipe));
    }

    [Fact, Trait("Lane", "unit")]
    public void Retry_on_a_child_wait_is_refused()
    {
        // The recipe of invalid/RCP-TIME-002.yaml; 'then: retry' is at 9:28.
        var recipe = Of(
            "running",
            [("running", WaitForChild("workflow.step", TimeoutAction.Retry)), ("done", Terminal())],
            [Go("running", "done", "wait.satisfied")],
            id: "workflow.time-retry");

        Assert.Equal(
            ["RCP-TIME-002: state 'running' retries a child wait when it times out at RCP-TIME-002.yaml:9:28 (states.running.timeout.then); use then: park or then: escalate, since a child can't be retried"],
            Lines(recipe, "RCP-TIME-002.yaml", [("states.running.timeout.then", 9, 28)]));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(TimeoutAction.Park)]
    [InlineData(TimeoutAction.Escalate)]
    [InlineData(TimeoutAction.Retry)]
    public void Timeouts_that_fit_their_state_raise_nothing(TimeoutAction then)
    {
        var recipe = Of(
            "produce",
            [("produce", Active(then: then)), ("claimed", WaitForEvent("claim.created", then: then)), ("running", WaitForChild("workflow.step", TimeoutAction.Escalate)), ("done", Terminal())],
            [Go("produce", "claimed"), Go("claimed", "running", "wait.satisfied"), Go("running", "done", "wait.satisfied")]);

        Assert.Empty(Lines(recipe));
    }
}
