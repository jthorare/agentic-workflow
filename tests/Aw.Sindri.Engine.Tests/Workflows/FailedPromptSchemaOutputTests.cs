using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;
using Temporalio.Testing;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>#3130: a prompt step whose JSON answer fails the schema check fails, and the run page still shows what the model said.</summary>
[Lane(TestLane.Unit)]
public sealed class FailedPromptSchemaOutputTests
{
    private const string Schema = """{"type":"object","required":["flagged"],"properties":{"flagged":{"type":"boolean"}}}""";

    [Fact]
    public async Task A_json_answer_missing_a_required_field_stores_the_raw_answer_as_its_output()
    {
        const string Answer = """{"other":1}""";

        var outcome = await Run(Answer);

        Assert.Equal(NodeRunStatus.Failed, outcome.Status);
        var output = Assert.IsType<JsonObject>(JsonNode.Parse(outcome.Output ?? "null"));
        Assert.Equal(Answer, output["answer"]?.GetValue<string>());
    }

    private static async Task<GraphBlockOutcome> Run(string answer)
    {
        var token = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var runs = new InMemoryWorkflowRunStore(clock);
        var workflows = new InMemoryWorkflowStore(clock);
        var workflow = WorkflowId.From("WF-00000000000000000000000000000001");
        var config = new JsonObject { ["prompt"] = "Check the day.", ["output"] = JsonNode.Parse(Schema) };
        var node = new GraphNode(NodeId.From("ask"), "prompt", Title: null, config);
        var spec = new GraphSpec(Version: 1, "Prompt test", Description: null, "unlimited", Resources: null, [node], []);
        _ = (await workflows.Create(workflow, WorkflowSlug.From("prompt-test"), spec, "owner", token)).Value;
        var run = new WorkflowRun(WorkflowRunId.From("prompt-test@1:1"), workflow, Revision: 1, """{"kind":"manual"}""", WorkflowRunStatus.Running, Outputs: null, Error: null, ItemId: null, clock.GetUtcNow(), EndedAt: null);
        _ = (await runs.Insert(run, token)).Value;
        var activities = new GraphBlockActivities(new GraphBlockRegistry([new PromptBlock(new FixedModel(answer))]), runs, workflows);

        return await new ActivityEnvironment().RunAsync(() => activities.RunBlockAsync(run.Id, node.Id, itemIndex: 0));
    }

    private sealed class FixedModel(string answer) : IGraphModelPort
    {
        public string Untrusted(string source, string text) => text;

        public ValueTask<Result<string>> Ask(string prompt, CancellationToken cancellationToken) => ValueTask.FromResult<Result<string>>(answer);
    }
}
