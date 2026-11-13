using System.Text;
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

/// <summary>P3e (#3196, spec §6.2, E4): a failed prompt step keeps the model text as raw on the node run, scrubbed by Aw.Redaction and cut at 64 KB.</summary>
[Lane(TestLane.Unit)]
public sealed class PromptRawOnNodeRunTests
{
    private const string Schema = """{"type":"object","required":["flagged"],"properties":{"flagged":{"type":"boolean"}}}""";

    // a GitHub token shape (prefix + 36 alphanumerics), built in two parts so no secret scanner flags the source
    private static readonly string _token = string.Concat("gh", "p_", new string('a', 36));

    [Fact]
    public async Task A_failed_prompt_step_carries_the_model_text_as_raw()
    {
        var outcome = await Run("Usage limit reached. Try again at 5pm.", exitCode: 1);

        Assert.Equal(NodeRunStatus.Failed, outcome.Status);
        Assert.Equal("Usage limit reached. Try again at 5pm.", outcome.Raw);
    }

    [Fact]
    public async Task A_succeeded_prompt_step_has_no_raw()
    {
        var outcome = await Run("""{"flagged":true}""", exitCode: 0);

        Assert.Equal(NodeRunStatus.Succeeded, outcome.Status);
        Assert.Null(outcome.Raw);
    }

    [Fact]
    public async Task A_token_in_the_model_text_is_scrubbed_from_raw_output_and_error()
    {
        var outcome = await Run($"Here is the key {_token} and nothing else.", exitCode: 1);

        Assert.NotNull(outcome.Raw);
        Assert.DoesNotContain(_token, outcome.Raw, StringComparison.Ordinal);
        Assert.Contains("[REDACTED", outcome.Raw, StringComparison.Ordinal);
        Assert.DoesNotContain(_token, outcome.Output ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(_token, outcome.Error ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Raw_is_cut_at_64_KB()
    {
        var outcome = await Run(new string('z', NodeRunLimits.PayloadBytes * 2), exitCode: 1);

        Assert.NotNull(outcome.Raw);
        Assert.True(Encoding.UTF8.GetByteCount(outcome.Raw) <= NodeRunLimits.PayloadBytes + 4);
    }

    [Fact]
    public async Task A_raw_text_over_the_cap_keeps_the_plain_cut()
    {
        // why: #3486, raw is model text, not JSON; it is never put in the JSON envelope. 'Truncated' is true here anyway: the same text is the failure output, and that cut sets it.
        var outcome = await Run(new string('z', NodeRunLimits.PayloadBytes * 2), exitCode: 1);

        Assert.StartsWith("zzzz", outcome.Raw, StringComparison.Ordinal);
        Assert.True(Encoding.UTF8.GetByteCount(outcome.Raw!) <= NodeRunLimits.PayloadBytes);
        Assert.False(outcome.Raw!.StartsWith('{'));
    }

    private static async Task<GraphBlockOutcome> Run(string answer, int exitCode)
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
        var activities = new GraphBlockActivities(new GraphBlockRegistry([new PromptBlock(new ExitModel(answer, exitCode))]), runs, workflows);

        return await new ActivityEnvironment().RunAsync(() => activities.RunBlockAsync(run.Id, node.Id, itemIndex: 0));
    }

    private sealed class ExitModel(string answer, int exitCode) : IGraphModelPort
    {
        public string Untrusted(string source, string text) => text;

        public ValueTask<Result<string>> Ask(string prompt, CancellationToken cancellationToken) => ValueTask.FromResult<Result<string>>(answer);

        ValueTask<Result<ModelAnswer>> IGraphModelPort.Answer(string prompt, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<ModelAnswer>>(new ModelAnswer(answer, exitCode));
    }
}
