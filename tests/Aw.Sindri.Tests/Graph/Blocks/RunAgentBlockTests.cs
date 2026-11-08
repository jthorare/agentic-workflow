using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Tests.Graph.Blocks;

[Lane(TestLane.Unit)]
public sealed class RunAgentBlockTests
{
    private static readonly LaunchId _session = LaunchId.From("LCH-0123456789abcdef0123456789abcdef");

    private static readonly RunAgentConfig _config = new(
        Repo: "client-repo",
        ReadOnlyRepos: ["api-repo"],
        Agent: "claude-code",
        Prompt: "Compare the contracts.",
        Outputs: new Dictionary<string, string>(StringComparer.Ordinal) { ["drifted"] = "boolean", ["prUrl"] = "string?", ["checkedCommit"] = "string" },
        Timeout: "45m",
        Connections: []);

    private static JsonObject NodeConfig() => new()
    {
        ["repo"] = "client-repo",
        ["readOnlyRepos"] = new JsonArray("api-repo"),
        ["agent"] = "claude-code",
        ["prompt"] = "Compare the contracts.",
        ["outputs"] = JsonNode.Parse("""{"drifted":"boolean","prUrl":"string?","checkedCommit":"string"}"""),
        ["timeout"] = "45m",
    };

    [Fact]
    public async Task A_report_that_matches_the_declared_outputs_completes_the_node()
    {
        var port = new ScriptedAgents(new AgentRunReport(
            JsonNode.Parse("""{"drifted":true,"checkedCommit":"abc123"}""")!.AsObject(),
            Status: "succeeded",
            Branch: "wf/morning-check/r1-4",
            Commit: "deadbeef",
            SessionId: _session,
            Usage: JsonNode.Parse("""{"tokens":1200,"costUsd":0.4}""")!.AsObject()));

        var result = await new RunAgentBlock(port).Execute(BlockFakes.Context(NodeConfig()), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var outputs = result.Value.Outputs;
        Assert.True(outputs["drifted"]?.GetValue<bool>());
        Assert.Equal("abc123", outputs["checkedCommit"]?.GetValue<string>());
        Assert.Equal("succeeded", outputs["status"]?.GetValue<string>());
        Assert.Equal("wf/morning-check/r1-4", outputs["branch"]?.GetValue<string>());
        Assert.Equal("deadbeef", outputs["commit"]?.GetValue<string>());
        Assert.Equal(_session.Value, outputs["sessionId"]?.GetValue<string>());
        Assert.Equal(1200, outputs["usage"]?["tokens"]?.GetValue<int>());
        var asked = Assert.Single(port.Requests);
        Assert.Equal("client-repo", asked.Config.Repo);
        Assert.Equal("claude-code", asked.Config.Agent);
        Assert.Equal("45m", asked.Config.Timeout);
    }

    [Fact]
    public void A_report_missing_a_declared_field_is_refused_and_the_agent_may_retry()
    {
        var report = JsonNode.Parse("""{"drifted":true}""")!.AsObject();

        var refused = RunAgentBlock.CheckReport(_config, report);
        var again = RunAgentBlock.CheckReport(_config, JsonNode.Parse("""{"drifted":true,"checkedCommit":"abc123"}""")!.AsObject());

        Assert.True(refused.IsError);
        Assert.Contains("checkedCommit", refused.Error.Message, StringComparison.Ordinal);
        Assert.True(again.IsSuccess);   // an optional field ('prUrl?') may be absent, and a corrected report is accepted
    }

    [Fact]
    public void A_declared_field_of_the_wrong_type_is_refused_naming_the_field()
    {
        var refused = RunAgentBlock.CheckReport(_config, JsonNode.Parse("""{"drifted":"yes","checkedCommit":"abc123"}""")!.AsObject());

        Assert.True(refused.IsError);
        Assert.Contains("drifted", refused.Error.Message, StringComparison.Ordinal);
        Assert.Contains("boolean", refused.Error.Message, StringComparison.Ordinal);
    }

    private sealed class ScriptedAgents(AgentRunReport report) : IAgentRunPort
    {
        public List<AgentRunRequest> Requests { get; } = [];

        public ValueTask<Result<AgentRunReport>> Run(AgentRunRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return ValueTask.FromResult<Result<AgentRunReport>>(report);
        }
    }
}
