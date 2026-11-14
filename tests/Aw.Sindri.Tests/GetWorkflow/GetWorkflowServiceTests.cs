using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Features.GetWorkflow;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.GetWorkflow;

/// <summary>'sindri wf show': the revision a workflow runs, who saved its newest revision, and a refusal for an unknown slug.</summary>
[Lane(TestLane.Unit)]
public sealed class GetWorkflowServiceTests
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("morning-check");

    private readonly FakeTimeProvider _clock = new();
    private readonly InMemoryWorkflowStore _workflows;
    private readonly GetWorkflowService _service;

    public GetWorkflowServiceTests()
    {
        _workflows = new InMemoryWorkflowStore(_clock);
        _service = new GetWorkflowService(_workflows, new Catalog(new ScheduleBlock(), new ManualBlock()));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_active_workflow_shows_its_active_revision_and_who_saved_the_newest()
    {
        await Create("Morning check");
        _ = (await _workflows.Activate(_id, 1, Token)).Value;
        _clock.Advance(TimeSpan.FromMinutes(5));
        var newest = (await _workflows.Save(_id, 1, Spec("Morning check v2"), "session:s1", Token)).Value;

        var result = await _service.Handle(new GetWorkflowRequest(_slug), Token);

        Assert.True(result.IsSuccess);
        var shown = result.Value;
        Assert.Equal((1, 1, 2), (shown.Revision, shown.ActiveRevision, shown.LatestRevision));
        Assert.Equal("Morning check", shown.Spec["name"]!.GetValue<string>());
        Assert.Equal(("owner", "session:s1", newest.CreatedAt), (shown.CreatedBy, shown.ChangedBy, shown.ChangedAt));
        Assert.Equal((_id, "Morning check v2"), (shown.Id, shown.Name));
        Assert.Equal((await _workflows.GetRevision(_id, 1, Token)).Value.SpecHash, shown.SpecHash);
    }

    [Fact]
    public async Task A_workflow_with_no_active_revision_shows_its_newest()
    {
        await Create("Morning check");
        _ = (await _workflows.Save(_id, 1, Spec("Morning check v2"), "owner", Token)).Value;

        var result = await _service.Handle(new GetWorkflowRequest(_slug), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Revision);
        Assert.Null(result.Value.ActiveRevision);
        Assert.Equal("Morning check v2", result.Value.Spec["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_newest_read_of_an_active_workflow_shows_the_saved_revision_and_still_says_which_one_runs()
    {
        await Create("Morning check");
        _ = (await _workflows.Activate(_id, 1, Token)).Value;
        _ = (await _workflows.Save(_id, 1, Spec("Morning check v2"), "session:s1", Token)).Value;

        var result = await _service.Handle(new GetWorkflowRequest(_slug, Revision: null, Newest: true), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal((2, 1, 2), (result.Value.Revision, result.Value.ActiveRevision, result.Value.LatestRevision));
        Assert.Equal("Morning check v2", result.Value.Spec["name"]!.GetValue<string>());
        Assert.Equal((await _workflows.GetRevision(_id, 2, Token)).Value.SpecHash, result.Value.SpecHash);
    }

    [Fact]
    public async Task A_revision_read_shows_that_revision_of_an_active_or_an_off_workflow()
    {
        await Create("Morning check");
        _ = (await _workflows.Save(_id, 1, Spec("Morning check v2"), "owner", Token)).Value;
        _ = (await _workflows.Save(_id, 2, Spec("Morning check v3"), "owner", Token)).Value;

        var first = await _service.Handle(new GetWorkflowRequest(_slug, Revision: 1), Token);
        var second = await _service.Handle(new GetWorkflowRequest(_slug, Revision: 2), Token);

        Assert.Equal((1, "Morning check"), (first.Value.Revision, first.Value.Spec["name"]!.GetValue<string>()));
        Assert.Equal((2, "Morning check v2"), (second.Value.Revision, second.Value.Spec["name"]!.GetValue<string>()));
        Assert.Equal(3, second.Value.LatestRevision);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3)]
    public async Task A_revision_the_workflow_does_not_have_is_not_found_and_never_the_active_spec(int revision)
    {
        await Create("Morning check");
        _ = (await _workflows.Activate(_id, 1, Token)).Value;
        _ = (await _workflows.Save(_id, 1, Spec("Morning check v2"), "owner", Token)).Value;

        var result = await _service.Handle(new GetWorkflowRequest(_slug, revision), Token);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.GraphNotFound.Code, result.Error.Code);
        Assert.Contains("revision " + revision.ToString(System.Globalization.CultureInfo.InvariantCulture), result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_slug_is_not_found()
    {
        var result = await _service.Handle(new GetWorkflowRequest(WorkflowSlug.From("nope")), Token);

        Assert.Equal(SindriErrors.GraphNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task A_schedule_node_on_a_preset_cron_reads_as_that_preset()
    {
        await CreateWith(Schedule("""{"cron":"0 7 * * 1-5","timezone":"America/Los_Angeles"}"""));

        var line = await SummaryOfStart();

        Assert.Equal(("Runs every weekday at 7:00, America/Los_Angeles", null), (line.Summary, line.SummaryProblem));
    }

    [Fact]
    public async Task A_schedule_node_on_a_custom_cron_shows_the_raw_cron()
    {
        await CreateWith(Schedule("""{"cron":"*/15 9-17 * * 1-5","timezone":"America/Los_Angeles"}"""));

        var line = await SummaryOfStart();

        Assert.Equal(("Runs on schedule */15 9-17 * * 1-5, America/Los_Angeles", null), (line.Summary, line.SummaryProblem));
    }

    [Fact]
    public async Task A_schedule_node_with_no_cron_shows_the_block_title_and_says_no_time_is_set()
    {
        await CreateWith(Schedule("{}"));

        var line = await SummaryOfStart();

        Assert.Equal(("Schedule", "No time set yet"), (line.Summary, line.SummaryProblem));
    }

    [Fact]
    public async Task A_node_of_another_block_shows_its_block_title()
    {
        await CreateWith(new GraphNode(NodeId.From("start"), "manual", Title: null, []));

        var line = await SummaryOfStart();

        Assert.Equal(("When called", null), (line.Summary, line.SummaryProblem));
    }

    private static GraphNode Schedule(string config) => new(NodeId.From("start"), "schedule", Title: null, JsonNode.Parse(config)!.AsObject());

    private async Task CreateWith(GraphNode node) =>
        _ = (await _workflows.Create(_id, _slug, new GraphSpec(Version: 1, "Morning check", Description: null, Concurrency: null, Resources: null, Nodes: [node], Edges: []), "owner", Token)).Value;

    private async Task<NodeSummary> SummaryOfStart()
    {
        var result = await _service.Handle(new GetWorkflowRequest(_slug), Token);
        Assert.True(result.IsSuccess);
        return result.Value.Summaries!.Single(s => s.Node == "start");
    }

    private sealed class Catalog(params IGraphBlockType[] blocks) : IGraphBlockCatalog
    {
        public IReadOnlyDictionary<string, IGraphBlockType> Types { get; } = blocks.ToDictionary(static b => b.Type, StringComparer.Ordinal);
    }

    private static GraphSpec Spec(string name) =>
        new(Version: 1, name, Description: null, Concurrency: null, Resources: null, Nodes: [new GraphNode(NodeId.From("start"), "manual", Title: null, [])], Edges: []);

    private async Task Create(string name) => _ = (await _workflows.Create(_id, _slug, Spec(name), "owner", Token)).Value;
}
