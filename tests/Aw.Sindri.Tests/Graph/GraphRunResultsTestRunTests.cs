using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Graph;

/// <summary>P4a (spec 2026-11-12 §3.2): 'workflow_result' never reads a test run, whether it asks for the newest succeeded run or names one.</summary>
[Lane(TestLane.Unit)]
public sealed class GraphRunResultsTestRunTests
{
    private static readonly WorkflowId _triageId = WorkflowId.From("WF-" + new string('1', 32));
    private static readonly WorkflowSlug _triage = WorkflowSlug.From("triage");

    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly InMemoryWorkflowRunStore _runs;
    private readonly InMemoryWorkflowStore _workflows;

    public GraphRunResultsTestRunTests()
    {
        _runs = new InMemoryWorkflowRunStore(_clock);
        _workflows = new InMemoryWorkflowStore(_clock);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task Define()
    {
        var spec = new GraphSpec(Version: 1, Name: "triage", Description: null, Concurrency: null, Resources: null, Nodes: [new GraphNode(NodeId.From("start"), "manual", Title: null, [])], Edges: []);
        _ = (await _workflows.Create(_triageId, _triage, spec, "owner", Token)).Value;
    }

    private async Task<WorkflowRunId> Succeeded(int occurrence, int minute, string mode, string outputs)
    {
        var id = WorkflowRunId.Of(_triage, 1, occurrence);
        var at = KnownInstants.Epoch.AddMinutes(minute);
        _ = (await _runs.Insert(new WorkflowRun(id, _triageId, 1, """{"kind":"manual"}""", WorkflowRunStatus.Succeeded, outputs, Error: null, ItemId: null, at, at.AddSeconds(30), mode), Token)).Value;
        return id;
    }

    [Fact]
    public async Task The_latest_succeeded_run_skips_a_newer_succeeded_test_run()
    {
        await Define();
        var live = await Succeeded(1, minute: 0, WorkflowRunMode.Live, """{"a":"live"}""");
        _ = await Succeeded(2, minute: 5, WorkflowRunMode.Test, """{"a":"test"}""");

        var result = await new GraphRunResults(_workflows, _runs).Read(_triage, run: null, Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Assert.Equal(live, result.Value.Run);
    }

    [Fact]
    public async Task A_workflow_with_only_test_runs_has_no_succeeded_run_to_read()
    {
        await Define();
        _ = await Succeeded(1, minute: 0, WorkflowRunMode.Test, """{"a":"test"}""");

        var result = await new GraphRunResults(_workflows, _runs).Read(_triage, run: null, Token);

        Assert.Equal(SindriErrors.GraphResultUnavailable.Code, result.Error.Code);
    }

    [Fact]
    public async Task Naming_a_test_run_by_id_is_refused()
    {
        await Define();
        var test = await Succeeded(1, minute: 0, WorkflowRunMode.Test, """{"a":"test"}""");

        var result = await new GraphRunResults(_workflows, _runs).Read(_triage, test, Token);

        Assert.Equal(SindriErrors.GraphResultUnavailable.Code, result.Error.Code);
    }
}
