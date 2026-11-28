using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Features.GetWorkflowRun;
using Aw.Sindri.Testing.Launch;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.GetWorkflowRun;

/// <summary>#3805: the run read's 'tokens' holds what the run's node runs used, summed over their usage, and its budget: the spec's, else the profile's, else null.</summary>
[Lane(TestLane.Unit)]
public sealed class GetWorkflowRunTokensTests
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("morning-check");
    private static readonly WorkflowRunId _run = WorkflowRunId.Of(_slug, 1, 7);

    private readonly FakeTimeProvider _clock = new();
    private readonly InMemoryWorkflowRunStore _runs;
    private readonly InMemoryWorkflowStore _workflows;
    private readonly Budget _budgets = new();

    public GetWorkflowRunTokensTests()
    {
        _runs = new InMemoryWorkflowRunStore(_clock);
        _workflows = new InMemoryWorkflowStore(_clock);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Used_is_the_sum_of_the_node_runs_usage_and_the_budget_is_the_specs()
    {
        await Insert(new GraphBudget(500), ("first", NodeRunUsage.Of(70)), ("second", NodeRunUsage.Of(50)), ("start", null));
        _budgets.Tokens = 9;

        var tokens = await Read();

        Assert.Equal(new RunTokens(Used: 120, Budget: 500), tokens);
    }

    [Fact]
    public async Task A_spec_without_a_budget_shows_the_profiles()
    {
        await Insert(budget: null, ("first", NodeRunUsage.Of(70)));
        _budgets.Tokens = 1000;

        Assert.Equal(new RunTokens(Used: 70, Budget: 1000), await Read());
    }

    [Fact]
    public async Task With_neither_the_budget_is_null()
    {
        await Insert(budget: null, ("first", NodeRunUsage.Of(70)));

        Assert.Equal(new RunTokens(Used: 70, Budget: null), await Read());
    }

    [Fact]
    public async Task A_usage_that_is_not_a_token_count_counts_as_nothing()
    {
        await Insert(budget: null, ("first", "{\"cost\":3}"), ("second", "not json"), ("third", NodeRunUsage.Of(5)));

        Assert.Equal(5L, (await Read()).Used);
    }

    private async Task<RunTokens> Read()
    {
        var service = new GetWorkflowRunService(_runs, _workflows, new FakeLaunchFiles(), new FakeAgentNodeControl(), _budgets);
        var result = await service.Handle(new GetWorkflowRunRequest(_run), Token);
        Assert.True(result.IsSuccess);
        return result.Value.Tokens;
    }

    private async Task Insert(GraphBudget? budget, params (string Node, string? Usage)[] nodes)
    {
        var spec = new GraphSpec(Version: 1, Name: "Morning check", Description: null, Concurrency: null, Resources: null,
            Nodes: [new GraphNode(NodeId.From("start"), "manual", Title: null, [])], Edges: []);
        _ = (await _workflows.Create(_id, _slug, spec with { Budget = budget }, "owner", Token)).Value;
        _ = (await _runs.Insert(new WorkflowRun(_run, _id, 1, "{\"kind\":\"manual\"}", WorkflowRunStatus.Succeeded, Outputs: null, Error: null, ItemId: null, _clock.GetUtcNow(), EndedAt: null), Token)).Value;
        foreach (var (node, usage) in nodes)
        {
            _ = (await _runs.RecordNodeRun(new NodeRun(_run, NodeId.From(node), 0, NodeRunStatus.Succeeded, Input: null, Output: "{}", Truncated: false, Error: null, usage), Token)).Value;
        }
    }

    private sealed class Budget : IRunBudgetSource
    {
        public long? Tokens { get; set; }

        public ValueTask<long?> DefaultTokens(CancellationToken cancellationToken) => ValueTask.FromResult(Tokens);
    }
}
