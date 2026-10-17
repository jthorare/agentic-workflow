using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Tests.Workflow.Choice;
using Aw.Sindri.Tests.Workflow.Engine;
using static Aw.Sindri.Tests.Workflow.Graph.GraphRecipe;

namespace Aw.Sindri.Tests.Workflow.Graph;

/// <summary>RCP-TIME-001 (every active and wait state has a timeout), RCP-TIME-002 (no retry on a child wait), spec §3.4, and RCP-TIME-003 (no retry on a block-runtime Step), DT spec §8.2.</summary>
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

    [Fact, Trait("Lane", "unit")]
    public void Retry_on_a_block_runtime_state_is_RCP_TIME_003()
    {
        var lines = RouteRecipes.Lines(RouteRecipes.Fixture("RCP-TIME-003"), "RCP-TIME-003.yaml");

        Assert.Equal(
            ["RCP-TIME-003: state 'route' runs step.judge.choose on the block runtime, where 'then: retry' doesn't apply at RCP-TIME-003.yaml:16:27 (states.route.timeout.then); use then: park (a judge call that runs out ends as step.failed, which the route sends to its desk twin)"],
            lines);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("then: park")]
    [InlineData("then: escalate")]
    public void Park_and_escalate_on_a_block_runtime_state_raise_nothing(string then)
    {
        var yaml = RouteRecipes.Fixture("RCP-TIME-003").Replace("then: retry", then, StringComparison.Ordinal);

        Assert.Empty(RouteRecipes.Lines(yaml, "RCP-TIME-003.yaml"));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("1m", 1)]
    [InlineData("2m", 1)]
    [InlineData("3m", 0)]
    [InlineData("2d", 0)]
    public void A_block_runtime_state_timeout_must_outlast_the_Steps_maxDuration(string after, int findings)
    {
        var yaml = RouteRecipes.Edit(RouteRecipes.Fixture("RCP-TIME-003"), "timeout: { after: 2d, then: retry }", $"timeout: {{ after: {after}, then: park }}");

        Assert.Equal(findings, RouteRecipes.Lines(yaml, "RCP-TIME-003.yaml").Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_Step_without_a_maxDuration_is_bounded_at_two_minutes_and_a_set_one_wins()
    {
        var judge = EngineFixture.Catalog.Find(BlockId.From("step.judge.choose"))!;

        Assert.Equal(TimeSpan.FromMinutes(2), BlockLimits.MaxDuration(judge with { MaxDuration = null }));
        Assert.Equal(TimeSpan.FromSeconds(5), BlockLimits.MaxDuration(judge with { MaxDuration = TimeSpan.FromSeconds(5) }));
    }
}
