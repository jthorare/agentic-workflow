using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Infrastructure.Workflows;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>#3229: a run_agent node that fails still leaves what the agent produced (its declared output files, its last refused report) as the node's output, so the run page shows it.</summary>
[Lane(TestLane.Integration)]
public sealed class FailedAgentOutputTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_failed_node_stores_the_declared_output_files_the_agent_wrote()
    {
        var runtime = new FakeAgentNodeRuntime
        {
            Recovered = new AgentNodeRecovered(Outputs: null, Reason: "the agent finished but its result was not recorded", Files: """{"summary":"half done","notes":{"a":1}}"""),
        };

        var row = await FailAs(runtime, new AgentSessionState(AgentSessionEnd.Exit, ExitCode: 1));

        Assert.Equal(NodeRunStatus.Failed, row.Status);
        var files = Assert.IsType<JsonObject>(JsonNode.Parse(row.Output ?? "null")!["files"]);
        Assert.Equal("half done", files["summary"]!.GetValue<string>());   // a file that is text stays text
        Assert.Equal(1, files["notes"]!["a"]!.GetValue<int>());            // a file that parses is its JSON value
        Assert.Null(JsonNode.Parse(row.Output!)?["lastRefusedReport"]);
    }

    [Fact]
    public async Task A_failed_node_whose_agent_left_nothing_keeps_a_null_output()
    {
        var row = await FailAs(new FakeAgentNodeRuntime(), new AgentSessionState(AgentSessionEnd.Exit, ExitCode: 1));

        Assert.Equal(NodeRunStatus.Failed, row.Status);
        Assert.Null(row.Output);
    }

    [Fact]
    public async Task A_failed_node_stores_the_last_refused_report()
    {
        var row = await FailAs(new FakeAgentNodeRuntime(), new AgentSessionState(AgentSessionEnd.Exit, ExitCode: 1, Detail: "the report is missing the field 'summary'", Refused: true));

        Assert.Equal(NodeRunStatus.Failed, row.Status);
        Assert.Equal("the report is missing the field 'summary'", JsonNode.Parse(row.Output ?? "null")!["lastRefusedReport"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_failed_node_whose_files_pass_64_KB_stores_parseable_json_marked_truncated()
    {
        // why: #3419, the failure output goes through the same fit as a succeeded output, so a big report can't land as cut, unparseable text
        var big = new string('x', 100 * 1024);
        var runtime = new FakeAgentNodeRuntime
        {
            Recovered = new AgentNodeRecovered(Outputs: null, Reason: "the agent finished but its result was not recorded", Files: $$"""{"summary":"{{big}}"}"""),
        };

        var row = await FailAs(runtime, new AgentSessionState(AgentSessionEnd.Exit, ExitCode: 1));

        Assert.Equal(NodeRunStatus.Failed, row.Status);
        Assert.True(row.Truncated);
        Assert.True(JsonNode.Parse(row.Output!)!["truncated"]!.GetValue<bool>());
    }

    private static async Task<NodeRun> FailAs(FakeAgentNodeRuntime runtime, AgentSessionState ended)
    {
        await using var rig = await GraphRig.StartAsync(runtime);
        var run = await AgentNodeRig.Start(rig, """{"drifted":"boolean"}""");
        await runtime.Watching.WaitAsync(TimeSpan.FromSeconds(30), Token);

        runtime.End(ended);
        _ = await rig.Final(run);
        return await AgentNodeRig.Row(rig, run);
    }
}
