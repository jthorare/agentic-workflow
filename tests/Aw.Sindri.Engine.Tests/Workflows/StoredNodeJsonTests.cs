using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Redaction;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Host;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;
using Temporalio.Testing;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>#3419: a node run's stored Input and Output are parsed as JSON downstream, so a 64 KB cut or a redactor block must still leave text that parses.</summary>
[Lane(TestLane.Unit)]
public sealed class StoredNodeJsonTests
{
    private static readonly WorkflowId _workflow = WorkflowId.From("WF-00000000000000000000000000000001");

    [Fact]
    public async Task A_code_node_output_over_64_KB_is_stored_as_json_that_parses()
    {
        var token = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var (runs, workflows, run) = await Seed(clock, token);
        var node = new GraphNode(NodeId.From("code1"), "code", Title: null, new JsonObject { ["code"] = "return big;" });
        var big = $"[\"{new string('x', 70 * 1024)}\"]";
        var activities = new GraphBlockActivities(new GraphBlockRegistry([new CodeBlock(new FixedCode(big))]), runs, workflows);

        var outcome = await new ActivityEnvironment().RunAsync(() => activities.RunBlockAsync(run.Id, node.Id, itemIndex: 0));

        Assert.Equal(NodeRunStatus.Succeeded, outcome.Status);
        Assert.NotNull(outcome.Output);
        Assert.NotNull(JsonNode.Parse(outcome.Output));
    }

    [Fact]
    public async Task A_redactor_blocked_input_is_stored_as_json_that_parses()
    {
        var token = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var (runs, workflows, run) = await Seed(clock, token);
        var activities = Recorder(clock, runs, workflows);
        var nodeRun = new NodeRun(run.Id, NodeId.From("code1"), ItemIndex: 0, NodeRunStatus.Running, """{"prompt":"flagged"}""", Output: null, Truncated: false, Error: null, Usage: null);

        await new ActivityEnvironment().RunAsync(() => activities.RecordNodeAsync(nodeRun));

        var stored = (await runs.NodeRuns(run.Id, token)).Value.Single();
        Assert.NotNull(stored.Input);
        Assert.NotNull(JsonNode.Parse(stored.Input));
    }

    [Fact]
    public async Task A_reference_to_a_redactor_blocked_output_fails_with_a_named_sentence()
    {
        var token = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var (runs, workflows, run) = await Seed(clock, token, withReader: true);
        var ask = new NodeRun(run.Id, NodeId.From("code1"), ItemIndex: 0, NodeRunStatus.Succeeded, Input: null, Output: StoredJson.Blocked, Truncated: false, Error: null, Usage: null);
        _ = (await runs.RecordNodeRun(ask, token)).Value;
        var activities = new GraphBlockActivities(new GraphBlockRegistry([new CodeBlock(new FixedCode("{}"))]), runs, workflows);

        var outcome = await new ActivityEnvironment().RunAsync(() => activities.RunBlockAsync(run.Id, NodeId.From("code2"), itemIndex: 0));

        Assert.Equal(NodeRunStatus.Failed, outcome.Status);
        Assert.Contains("the output of 'code1' was blocked by the redactor", outcome.Error, StringComparison.Ordinal);
    }

    private static GraphRunActivities Recorder(FakeTimeProvider clock, InMemoryWorkflowRunStore runs, InMemoryWorkflowStore workflows)
    {
        var token = TestContext.Current.CancellationToken;
        var probe = new FakeProcessProbe();
        var ledger = new InMemorySindriStore(clock, probe);
        var held = new HeldLease();
        held.Hold(ledger.Acquire(probe.Self(), token).AsTask().GetAwaiter().GetResult().Value);
        var ids = new SequentialIdGenerator();
        var events = new EventFactory(EventScope.ControlPlane, clock, ids, AwJson.CreateOptions(SindriJsonContext.Default), new AsyncLocalCallerContextAccessor(ids));
        return new GraphRunActivities(runs, workflows, new NoQueue(), new GraphMirror(ledger, held, events, new BlockingRedactor()));
    }

    private static async Task<(InMemoryWorkflowRunStore Runs, InMemoryWorkflowStore Workflows, WorkflowRun Run)> Seed(FakeTimeProvider clock, CancellationToken token, bool withReader = false)
    {
        var runs = new InMemoryWorkflowRunStore(clock);
        var workflows = new InMemoryWorkflowStore(clock);
        var node = new GraphNode(NodeId.From("code1"), "code", Title: null, new JsonObject { ["code"] = "return big;" });
        var spec = new GraphSpec(Version: 1, "Stored json test", Description: null, "unlimited", Resources: null, withReader ? [node, new GraphNode(NodeId.From("code2"), "code", Title: null, new JsonObject { ["code"] = "return {{code1.answer}};" })] : [node], []);
        _ = (await workflows.Create(_workflow, WorkflowSlug.From("stored-json-test"), spec, "owner", token)).Value;
        var run = new WorkflowRun(WorkflowRunId.From("stored-json-test@1:1"), _workflow, Revision: 1, """{"kind":"manual"}""", WorkflowRunStatus.Running, Outputs: null, Error: null, ItemId: null, clock.GetUtcNow(), EndedAt: null);
        _ = (await runs.Insert(run, token)).Value;
        return (runs, workflows, run);
    }

    private sealed class FixedCode(string json) : ICodeRunPort
    {
        public ValueTask<Result<string>> Run(CodeRun run, CancellationToken cancellationToken) => ValueTask.FromResult<Result<string>>(json);
    }

    private sealed class BlockingRedactor : IRedactor
    {
        public ScrubResult Scrub(string text) => new(string.Empty, [], ScrubOutcome.Blocked, "TEST-BLOCKED");

        public bool IsSecretName(string name) => false;
    }

    private sealed class NoQueue : IGraphQueueAdvance
    {
        public ValueTask<Result<WorkflowRunId?>> StartNextQueued(WorkflowId workflow, CancellationToken cancellationToken) => ValueTask.FromResult<Result<WorkflowRunId?>>((WorkflowRunId?)null);

        public ValueTask<Result<IReadOnlyList<WorkflowRun>>> RunningBeforeThisProcess(CancellationToken cancellationToken) => ValueTask.FromResult<Result<IReadOnlyList<WorkflowRun>>>(Array.Empty<WorkflowRun>());

        public ValueTask<Result<IReadOnlyList<WorkflowRun>>> RunningNotLaunching(CancellationToken cancellationToken) => ValueTask.FromResult<Result<IReadOnlyList<WorkflowRun>>>(Array.Empty<WorkflowRun>());
    }
}
