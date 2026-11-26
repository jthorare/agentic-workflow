using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Features.ListWorkflowRuns;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.ListWorkflowRuns;

/// <summary>#3304 (spec §6.1, §7.2): a failed run's row carries its sentence, naming the step by its title, so the runs list never shows "node 'check' failed: SND-CODE-...".</summary>
[Lane(TestLane.Unit)]
public sealed class ListWorkflowRunsFailureTests
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("size-check");

    private readonly FakeTimeProvider _clock = new();
    private readonly InMemoryWorkflowStore _workflows;
    private readonly InMemoryWorkflowRunStore _runs;
    private readonly ListWorkflowRunsService _service;

    public ListWorkflowRunsFailureTests()
    {
        _workflows = new InMemoryWorkflowStore(_clock);
        _runs = new InMemoryWorkflowRunStore(_clock);
        _service = new ListWorkflowRunsService(_workflows, _runs);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_failed_run_row_says_its_step_stopped_and_keeps_the_engine_error()
    {
        await Create();
        const string Error = "node 'check' failed: SND-CODE-004: InvalidOperationException: repo is 812 MB, over the 500 MB budget";
        await Insert(1, WorkflowRunStatus.Failed, Error);

        var row = Assert.Single((await _service.Handle(new ListWorkflowRunsRequest(_slug), Token)).Value.Runs);

        Assert.Equal("Check stopped: repo is 812 MB, over the 500 MB budget.", row.FailureSentence);
        Assert.Equal(Error, row.Error);
    }

    [Fact]
    public async Task A_succeeded_run_or_an_error_with_no_sentence_has_no_failure_sentence()
    {
        await Create();
        await Insert(1, WorkflowRunStatus.Succeeded, error: null);
        await Insert(2, WorkflowRunStatus.Failed, "node 'check' failed: SND-NOPE-999: boom");

        var rows = (await _service.Handle(new ListWorkflowRunsRequest(_slug), Token)).Value.Runs;

        Assert.All(rows, static r => Assert.Null(r.FailureSentence));
    }

    private async Task Create()
    {
        var spec = new GraphSpec(Version: 1, Name: "Size check", Description: null, Concurrency: null, Resources: null,
            Nodes: [new GraphNode(NodeId.From("start"), "manual", Title: null, []), new GraphNode(NodeId.From("check"), "code", "Check", [])],
            Edges: [new GraphEdge(NodeId.From("start"), Output: null, NodeId.From("check"))]);
        _ = (await _workflows.Create(_id, _slug, spec, "owner", Token)).Value;
    }

    private async Task Insert(int occurrence, WorkflowRunStatus status, string? error)
    {
        _clock.Advance(TimeSpan.FromMinutes(1));
        _ = (await _runs.Insert(new WorkflowRun(WorkflowRunId.Of(_slug, 1, occurrence), _id, 1, "{\"kind\":\"manual\"}", status, Outputs: null, error, ItemId: null, _clock.GetUtcNow(), EndedAt: null), Token)).Value;
    }
}
