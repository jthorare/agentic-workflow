using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Redaction;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;
using Temporalio.Testing;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>#3376: a succeeded node whose output the redactor blocks stores valid JSON, so a downstream reference that parses the stored output does not throw.</summary>
[Lane(TestLane.Unit)]
public sealed class BlockedOutputReferenceTests
{
    private const string Marker = "[blocked by the redactor]";

    [Fact]
    public async Task A_succeeded_node_whose_output_is_blocked_stores_output_a_downstream_reference_can_parse()
    {
        var outcome = await Run("""{"flagged":true}""");

        Assert.Equal(NodeRunStatus.Succeeded, outcome.Status);
        var parsed = JsonNode.Parse(outcome.Output ?? "null");
        Assert.Equal(Marker, parsed?.GetValue<string>());
    }

    private static async Task<GraphBlockOutcome> Run(string answer)
    {
        var token = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var runs = new InMemoryWorkflowRunStore(clock);
        var workflows = new InMemoryWorkflowStore(clock);
        var workflow = WorkflowId.From("WF-00000000000000000000000000000001");
        var config = new JsonObject { ["prompt"] = "Check the day.", ["output"] = JsonNode.Parse("""{"type":"object","required":["flagged"],"properties":{"flagged":{"type":"boolean"}}}""") };
        var node = new GraphNode(NodeId.From("ask"), "prompt", Title: null, config);
        var spec = new GraphSpec(Version: 1, "Blocked test", Description: null, "unlimited", Resources: null, [node], []);
        _ = (await workflows.Create(workflow, WorkflowSlug.From("blocked-test"), spec, "owner", token)).Value;
        var run = new WorkflowRun(WorkflowRunId.From("blocked-test@1:1"), workflow, Revision: 1, """{"kind":"manual"}""", WorkflowRunStatus.Running, Outputs: null, Error: null, ItemId: null, clock.GetUtcNow(), EndedAt: null);
        _ = (await runs.Insert(run, token)).Value;
        var activities = new GraphBlockActivities(new GraphBlockRegistry([new PromptBlock(new FixedModel(answer))]), runs, workflows, redactor: new BlockingRedactor());

        return await new ActivityEnvironment().RunAsync(() => activities.RunBlockAsync(run.Id, node.Id, itemIndex: 0));
    }

    private sealed class FixedModel(string answer) : IGraphModelPort
    {
        public string Untrusted(string source, string text) => text;

        public ValueTask<Result<string>> Ask(string prompt, CancellationToken cancellationToken) => ValueTask.FromResult<Result<string>>(answer);
    }

    private sealed class BlockingRedactor : IRedactor
    {
        public ScrubResult Scrub(string text) => new(string.Empty, [], ScrubOutcome.Blocked, "TEST-BLOCKED");

        public bool IsSecretName(string name) => false;
    }
}
