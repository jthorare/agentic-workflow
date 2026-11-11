using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Graph;

/// <summary>What a 'workflow_result' node reads (agent-first PR 9, #2258): the newest succeeded run, or one finished run of that workflow by id; it reads and starts nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class GraphRunResultsTests
{
    private static readonly WorkflowId _triageId = WorkflowId.From("WF-" + new string('1', 32));
    private static readonly WorkflowId _otherId = WorkflowId.From("WF-" + new string('2', 32));
    private static readonly WorkflowSlug _triage = WorkflowSlug.From("triage");
    private static readonly WorkflowSlug _other = WorkflowSlug.From("other");

    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly InMemoryWorkflowRunStore _runs;
    private readonly InMemoryWorkflowStore _workflows;

    public GraphRunResultsTests()
    {
        _runs = new InMemoryWorkflowRunStore(_clock);
        _workflows = new InMemoryWorkflowStore(_clock);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private GraphRunResults Results() => new(_workflows, _runs);

    private async Task Define(WorkflowId id, WorkflowSlug slug)
    {
        var spec = new GraphSpec(Version: 1, Name: slug.Value, Description: null, Concurrency: null, Resources: null, Nodes: [new GraphNode(NodeId.From("start"), "manual", Title: null, [])], Edges: []);
        _ = (await _workflows.Create(id, slug, spec, "owner", Token)).Value;
    }

    private async Task<WorkflowRunId> Run(WorkflowId workflow, WorkflowSlug slug, int occurrence, WorkflowRunStatus status, int minute, string? outputs = null)
    {
        var id = WorkflowRunId.Of(slug, 1, occurrence);
        var ended = status is WorkflowRunStatus.Succeeded or WorkflowRunStatus.Failed or WorkflowRunStatus.Cancelled;
        var at = KnownInstants.Epoch.AddMinutes(minute);
        _ = (await _runs.Insert(new WorkflowRun(id, workflow, 1, """{"kind":"manual"}""", status, outputs, Error: null, ItemId: null, at, ended ? at.AddSeconds(30) : null), Token)).Value;
        return id;
    }

    [Fact]
    public async Task The_latest_succeeded_run_is_the_newest_one_that_succeeded_and_nothing_starts()
    {
        await Define(_triageId, _triage);
        _ = await Run(_triageId, _triage, 1, WorkflowRunStatus.Succeeded, minute: 0, outputs: """{"a":1}""");
        _ = await Run(_triageId, _triage, 2, WorkflowRunStatus.Failed, minute: 1);
        var newest = await Run(_triageId, _triage, 3, WorkflowRunStatus.Succeeded, minute: 2, outputs: """{"a":3}""");
        _ = await Run(_triageId, _triage, 4, WorkflowRunStatus.Running, minute: 3);

        var result = await Results().Read(_triage, run: null, Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Assert.Equal((newest, "succeeded"), (result.Value.Run, result.Value.Status));
        Assert.Equal(3, result.Value.Outputs!["a"]!.GetValue<int>());
        Assert.Equal(KnownInstants.Epoch.AddMinutes(2).AddSeconds(30), result.Value.FinishedAt);
        Assert.Equal(4, (await _runs.Recent(_triageId, 0, 50, Token)).Value.Count);   // nothing was started
    }

    [Fact]
    public async Task A_workflow_with_no_succeeded_run_is_refused()
    {
        await Define(_triageId, _triage);
        _ = await Run(_triageId, _triage, 1, WorkflowRunStatus.Failed, minute: 0);

        var result = await Results().Read(_triage, run: null, Token);

        Assert.Equal(SindriErrors.GraphResultUnavailable.Code, result.Error.Code);
    }

    [Fact]
    public async Task A_run_id_of_another_workflow_is_refused()
    {
        await Define(_triageId, _triage);
        await Define(_otherId, _other);
        var foreign = await Run(_otherId, _other, 1, WorkflowRunStatus.Succeeded, minute: 0);

        var result = await Results().Read(_triage, foreign, Token);

        Assert.Equal(SindriErrors.GraphResultUnavailable.Code, result.Error.Code);
    }

    [Fact]
    public async Task A_run_that_is_not_finished_is_refused()
    {
        await Define(_triageId, _triage);
        var running = await Run(_triageId, _triage, 1, WorkflowRunStatus.Running, minute: 0);

        var result = await Results().Read(_triage, running, Token);

        Assert.Equal(SindriErrors.GraphResultUnavailable.Code, result.Error.Code);
    }

    [Fact]
    public async Task A_finished_run_that_failed_is_read_by_id_with_its_status_and_no_outputs()
    {
        await Define(_triageId, _triage);
        var failed = await Run(_triageId, _triage, 1, WorkflowRunStatus.Failed, minute: 0);

        var result = await Results().Read(_triage, failed, Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Assert.Equal((failed, "failed"), (result.Value.Run, result.Value.Status));
        Assert.Null(result.Value.Outputs);
    }

    [Fact]
    public async Task An_unknown_workflow_is_not_found_and_an_unknown_run_is_refused()
    {
        await Define(_triageId, _triage);

        var workflow = await Results().Read(_other, run: null, Token);
        var run = await Results().Read(_triage, WorkflowRunId.Of(_triage, 1, 99), Token);

        Assert.Equal(SindriErrors.GraphNotFound.Code, workflow.Error.Code);
        Assert.Equal(SindriErrors.GraphResultUnavailable.Code, run.Error.Code);
    }
}
