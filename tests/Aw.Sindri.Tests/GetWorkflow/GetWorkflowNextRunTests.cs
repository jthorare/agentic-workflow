using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Features.GetWorkflow;
using Aw.Sindri.Features.ListWorkflows;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.GetWorkflow;

/// <summary>Issue 3756: GET /sindri/workflows/{slug} says when an active schedule next runs, the same value as its list row. The clock reads Sat 14 Nov 2026 10:42 UTC.</summary>
[Lane(TestLane.Unit)]
public sealed class GetWorkflowNextRunTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 14, 10, 42, 0, TimeSpan.Zero);
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("morning-check");

    private readonly FakeTimeProvider _clock = new(_now);
    private readonly InMemoryWorkflowStore _workflows;
    private readonly GetWorkflowService _get;
    private readonly ListWorkflowsService _list;

    public GetWorkflowNextRunTests()
    {
        _workflows = new InMemoryWorkflowStore(_clock);
        _get = new GetWorkflowService(_workflows, new Catalog(new ScheduleBlock(), new ManualBlock()), _clock);
        _list = new ListWorkflowsService(_workflows, new InMemoryWorkflowRunStore(_clock), _clock);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_active_schedule_says_its_next_run()
    {
        await Create(Schedule("0 7 * * *", "UTC"));
        _ = (await _workflows.Activate(_id, 1, Token)).Value;

        Assert.Equal(new DateTimeOffset(2026, 11, 15, 7, 0, 0, TimeSpan.Zero), await NextRun());
    }

    [Fact]
    public async Task A_paused_workflow_has_no_next_run()
    {
        await Create(Schedule("0 7 * * *", "UTC"));
        _ = (await _workflows.Activate(_id, 1, Token)).Value;
        _ = (await _workflows.Pause(_id, Token)).Value;

        Assert.Null(await NextRun());
    }

    [Fact]
    public async Task A_never_activated_workflow_has_no_next_run()
    {
        await Create(Schedule("0 7 * * *", "UTC"));

        Assert.Null(await NextRun());
    }

    [Fact]
    public async Task An_archived_workflow_has_no_next_run()
    {
        await Create(Schedule("0 7 * * *", "UTC"));
        _ = (await _workflows.Activate(_id, 1, Token)).Value;
        _ = (await _workflows.Archive(_id, Token)).Value;

        Assert.Null(await NextRun());
    }

    [Theory]
    [InlineData("manual")]
    [InlineData("connection_event")]
    public async Task A_manual_or_event_trigger_has_no_next_run(string type)
    {
        await Create(new GraphNode(NodeId.From("start"), type, Title: null, []));
        _ = (await _workflows.Activate(_id, 1, Token)).Value;

        Assert.Null(await NextRun());
    }

    [Fact]
    public async Task A_schedule_with_no_tick_in_years_has_no_next_run_and_the_read_still_answers()
    {
        await Create(Schedule("0 7 30 2 *", "UTC"));   // 30 February never comes
        _ = (await _workflows.Activate(_id, 1, Token)).Value;

        Assert.Null(await NextRun());
    }

    [Theory]
    [InlineData("0 7 * * *", "UTC", "2026-11-15T07:00:00Z")]
    [InlineData("0 7 * * 1-5", "America/Los_Angeles", "2026-11-16T15:00:00Z")]   // Sat -> Mon 7:00 PST
    [InlineData("*/20 * * * *", "UTC", "2026-11-14T11:00:00Z")]
    public async Task Get_and_list_say_the_same_next_run_for_the_same_workflow(string cron, string timezone, string expected)
    {
        await Create(Schedule(cron, timezone));
        _ = (await _workflows.Activate(_id, 1, Token)).Value;

        var fromGet = await NextRun();
        var fromList = (await _list.Handle(new ListWorkflowsRequest(), Token)).Value.Workflows.Single().NextRunAt;

        Assert.Equal(DateTimeOffset.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), fromGet);
        Assert.Equal(fromList, fromGet);
    }

    private static GraphNode Schedule(string cron, string timezone) =>
        new(NodeId.From("start"), "schedule", Title: null, new JsonObject { ["cron"] = cron, ["timezone"] = timezone });

    private async Task Create(GraphNode trigger) =>
        _ = (await _workflows.Create(_id, _slug, new GraphSpec(Version: 1, "Morning check", Description: null, Concurrency: null, Resources: null, Nodes: [trigger], Edges: []), "owner", Token)).Value;

    private async Task<DateTimeOffset?> NextRun()
    {
        var result = await _get.Handle(new GetWorkflowRequest(_slug), Token);
        Assert.True(result.IsSuccess);
        return result.Value.NextRunAt;
    }

    private sealed class Catalog(params IGraphBlockType[] blocks) : IGraphBlockCatalog
    {
        public IReadOnlyDictionary<string, IGraphBlockType> Types { get; } = blocks.ToDictionary(static b => b.Type, StringComparer.Ordinal);
    }
}
