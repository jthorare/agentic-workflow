using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Tests.Graph.Blocks;

/// <summary>The 'run_agent' block (PR 7): its descriptor, the check a node's report goes through, and what a finished node hands on.</summary>
[Lane(TestLane.Unit)]
public sealed class RunAgentBlockTests
{
    private static readonly Dictionary<string, string> _declared = new(StringComparer.Ordinal) { ["drifted"] = "boolean", ["summary"] = "string", ["notes"] = "string?" };

    private static Dictionary<string, ReadOnlyMemory<byte>> Report(params (string Field, string Content)[] fields) =>
        fields.ToDictionary(static f => f.Field, static f => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(f.Content), StringComparer.Ordinal);

    [Fact]
    public void A_report_that_matches_the_declared_outputs_completes_the_node()
    {
        var report = RunAgentReport.Check(_declared, Report(("drifted", "true"), ("summary", "two files changed\n")));
        Assert.True(report.IsSuccess, report.IsError ? report.Error.Message : null);

        var result = RunAgentOutput.Compose(report.Value, new AgentNodeEnd("wf/walk/1-1-check", "abc123", "agent-session-1", 1200, EmptyDiff: false));

        Assert.True(result.Outputs["drifted"]!.GetValue<bool>());
        Assert.Equal("two files changed", result.Outputs["summary"]!.GetValue<string>());   // a string output is the file's text without its last newline
        Assert.Null(result.Outputs["notes"]);                                              // an optional field the agent left out is there, empty
        Assert.Equal("succeeded", result.Outputs["status"]!.GetValue<string>());
        Assert.Equal("wf/walk/1-1-check", result.Outputs["branch"]!.GetValue<string>());
        Assert.Equal("abc123", result.Outputs["commit"]!.GetValue<string>());
        Assert.Equal("agent-session-1", result.Outputs["sessionId"]!.GetValue<string>());
        Assert.Equal(1200, result.Outputs["usage"]!["tokens"]!.GetValue<int>());
        Assert.False(result.Outputs["usage"]!["emptyDiff"]!.GetValue<bool>());
        Assert.Equal(["out"], result.Taken);
    }

    [Fact]
    public void A_report_missing_a_declared_field_is_refused_and_the_agent_may_retry()
    {
        var refused = RunAgentReport.Check(_declared, Report(("drifted", "true")));

        Assert.True(refused.IsError);
        Assert.Equal(SindriErrors.InvalidInput.Code, refused.Error.Code);
        Assert.Contains("summary", refused.Error.Message, StringComparison.Ordinal);       // the message names the field
        Assert.DoesNotContain("notes", refused.Error.Message, StringComparison.Ordinal);   // an optional field is not asked for

        var again = RunAgentReport.Check(_declared, Report(("drifted", "false"), ("summary", "clean")));   // nothing was kept from the refused report

        Assert.True(again.IsSuccess);
    }

    [Fact]
    public void A_field_that_is_not_declared_or_has_the_wrong_type_is_refused_by_name()
    {
        var extra = RunAgentReport.Check(_declared, Report(("drifted", "true"), ("summary", "x"), ("surprise", "1")));
        var typed = RunAgentReport.Check(_declared, Report(("drifted", "maybe"), ("summary", "x")));

        Assert.True(extra.IsError);
        Assert.Contains("surprise", extra.Error.Message, StringComparison.Ordinal);
        Assert.True(typed.IsError);
        Assert.Contains("drifted", typed.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_block_hands_on_the_declared_fields_and_the_standard_ones()
    {
        var config = JsonNode.Parse("""{"repo":"web","prompt":"p","outputs":{"drifted":"boolean","notes":"string?"}}""")!.AsObject();

        var outputs = new RunAgentBlock().OutputsFor(config);

        Assert.Equal("boolean", outputs.Fields["drifted"]);
        Assert.Equal("string?", outputs.Fields["notes"]);
        Assert.Equal(["branch", "commit", "drifted", "notes", "sessionId", "status", "usage"], outputs.Fields.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(["out"], outputs.BranchNames);
    }

    [Fact]
    public void The_block_checks_what_the_field_types_cannot()
    {
        var block = new RunAgentBlock();

        Assert.Empty(block.CheckConfig(JsonNode.Parse(block.Example)!.AsObject()));
        Assert.Contains(block.CheckConfig(JsonNode.Parse("""{"repo":"web","prompt":"p","agent":"gpt"}""")!.AsObject()), p => p.Field == "agent");
        Assert.Contains(block.CheckConfig(JsonNode.Parse("""{"repo":"web","prompt":"p","outputs":{"branch":"string"}}""")!.AsObject()), p => p.Field == "outputs");   // a standard name
        Assert.Contains(block.CheckConfig(JsonNode.Parse("""{"repo":"web","prompt":"p","outputs":{"Bad_Name":"string"}}""")!.AsObject()), p => p.Field == "outputs");
        Assert.Contains(block.CheckConfig(JsonNode.Parse("""{"repo":"web","prompt":"p","outputs":{"a":"date"}}""")!.AsObject()), p => p.Field == "outputs");
        Assert.Contains(block.CheckConfig(JsonNode.Parse("""{"repo":"web","prompt":"p","timeout":"soon"}""")!.AsObject()), p => p.Field == "timeout");
        Assert.Contains(block.CheckConfig(JsonNode.Parse("""{"repo":"web","prompt":"p","agent":"codex","readOnlyRepos":["api"]}""")!.AsObject()), p => p.Field == "readOnlyRepos");
        Assert.DoesNotContain(block.CheckConfig(JsonNode.Parse("""{"repo":"web","prompt":"p","agent":"claude-code","readOnlyRepos":["api"]}""")!.AsObject()), p => p.Field == "readOnlyRepos");
        Assert.Empty(block.Config.Check(JsonNode.Parse(block.Example)!.AsObject()));
    }

    [Fact]
    public void The_config_reads_into_its_parts()
    {
        var config = RunAgentConfig.Parse(JsonNode.Parse("""{"repo":"web","readOnlyRepos":["api"],"agent":"codex","prompt":"go","outputs":{"ok":"boolean"},"timeout":"45m","connections":["linear"],"resources":{"wall":"30m"}}""")!.AsObject());

        Assert.True(config.IsSuccess, config.IsError ? config.Error.Message : null);
        Assert.Equal("web", config.Value.Repo);
        Assert.Equal(["api"], config.Value.ReadOnlyRepos);
        Assert.Equal("codex", config.Value.Agent);
        Assert.Equal(TimeSpan.FromMinutes(45), config.Value.Timeout);
        Assert.Equal(TimeSpan.FromMinutes(30), config.Value.Wall);
        Assert.Equal(["linear"], config.Value.Connections);
        Assert.Equal(RunAgentConfig.DefaultTimeout, RunAgentConfig.Parse(JsonNode.Parse("""{"repo":"web","prompt":"go"}""")!.AsObject()).Value.Timeout);
    }

    [Fact]
    public async Task Executing_it_as_a_plain_block_fails_because_the_walk_runs_it_in_the_agent_loop()
    {
        var block = new RunAgentBlock();
        var context = BlockFakes.Context(JsonNode.Parse(block.Example)!.AsObject());

        var result = await block.Execute(context, TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
    }

    [Fact]
    public void The_standard_registry_has_the_block()
    {
        var registry = GraphBlockRegistry.Standard(new BlockFakes.RecordingNotifyPort(), new BlockFakes.ScriptedModelPort("{}"));

        _ = Assert.IsType<RunAgentBlock>(registry.Find("run_agent"));
    }
}
