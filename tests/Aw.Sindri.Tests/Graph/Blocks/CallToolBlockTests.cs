using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Toolkits;
using Aw.Sindri.Tests.Toolkits;

namespace Aw.Sindri.Tests.Graph.Blocks;

[Lane(TestLane.Unit)]
public sealed class CallToolBlockTests
{
    private const string AddNote = """{"type":"object","properties":{"text":{"type":"string"},"count":{"type":"integer"}},"required":["text"]}""";

    private static FakeRunningToolkits Runtime() => new(
        [FakeRunningToolkits.Make("notes", ToolkitState.Running, ("add_note", AddNote)), FakeRunningToolkits.Make("slow", ToolkitState.Paused, ("crawl", """{"type":"object"}"""))],
        (_, _, args) => $"{{\"saved\":true,\"id\":7,\"args\":{args}}}");

    private sealed class FakeConnections : IConnectionToolPort
    {
        public List<(string Connection, string Tool, string Arguments)> Calls { get; } = [];

        public ValueTask<Result<IReadOnlyList<ConnectionTool>>> ListTools(string connection, CancellationToken cancellationToken)
        {
            var answer = connection switch
            {
                "linear" => Results.Success<IReadOnlyList<ConnectionTool>>([new("search_issues", "Search issues.", "mcp")]),
                "github" => Results.Success<IReadOnlyList<ConnectionTool>>([new("create_issue", "Open an issue.", "rest")]),
                _ => Results.Failure<IReadOnlyList<ConnectionTool>>(SindriErrors.ConnectionNotFound.With($"no connection '{connection}'")),
            };
            return ValueTask.FromResult(answer);
        }

        public ValueTask<Result<string>> Call(string connection, string tool, string argumentsJson, CancellationToken cancellationToken)
        {
            Calls.Add((connection, tool, argumentsJson));
            return ValueTask.FromResult<Result<string>>($"{{\"via\":\"{connection}\",\"tool\":\"{tool}\"}}");
        }
    }

    [Fact]
    public async Task A_workflow_calls_a_toolkit_tool_and_its_JSON_result_feeds_the_next_node()
    {
        var runtime = Runtime();
        var block = new CallToolBlock(runtime, new FakeConnections());
        var config = new JsonObject { ["tool"] = "notes__add_note", ["arguments"] = new JsonObject { ["text"] = "hello", ["count"] = 2 } };

        var run = await block.Execute(BlockFakes.Context(config), TestContext.Current.CancellationToken);

        Assert.True(run.IsSuccess, run.IsError ? run.Error.Message : null);
        var result = run.Value.Outputs["result"]!.AsObject();
        Assert.True((bool)result["saved"]!);
        Assert.Equal(7, (int)result["id"]!);
        Assert.Equal("""{"text":"hello","count":2}""", runtime.Calls.Single().Arguments);
        Assert.StartsWith("workflow:", runtime.Calls.Single().Caller, StringComparison.Ordinal);
        Assert.Contains(block.OutputsFor(config).Fields.Keys, key => key == "result");
    }

    [Fact]
    public void The_validator_checks_the_tool_name_and_the_arguments_against_the_manifest_schema()
    {
        var block = new CallToolBlock(Runtime(), new FakeConnections());

        var fine = block.CheckConfig(new JsonObject { ["tool"] = "notes__add_note", ["arguments"] = new JsonObject { ["text"] = "x" } });
        var noTool = block.CheckConfig(new JsonObject { ["tool"] = "notes__missing" });
        var missingRequired = block.CheckConfig(new JsonObject { ["tool"] = "notes__add_note", ["arguments"] = new JsonObject { ["count"] = 1 } });
        var wrongType = block.CheckConfig(new JsonObject { ["tool"] = "notes__add_note", ["arguments"] = new JsonObject { ["text"] = 5 } });
        var reference = block.CheckConfig(new JsonObject { ["tool"] = "notes__add_note", ["arguments"] = new JsonObject { ["text"] = "{{triage.summary}}", ["count"] = "{{triage.n}}" } });
        var malformed = block.CheckConfig(new JsonObject { ["tool"] = "not a name" });

        Assert.Empty(fine);
        Assert.Contains(noTool, p => p.Field == "tool" && p.Problem.Contains("add_note", StringComparison.Ordinal));
        Assert.Contains(missingRequired, p => p.Field == "arguments" && p.Problem.Contains("text", StringComparison.Ordinal));
        Assert.Contains(wrongType, p => p.Field == "arguments" && p.Problem.Contains("text", StringComparison.Ordinal));
        Assert.Empty(reference);
        Assert.Contains(malformed, p => p.Field == "tool");
    }

    [Fact]
    public async Task A_paused_toolkit_and_a_bad_argument_fail_the_node_without_calling_the_tool()
    {
        var runtime = Runtime();
        var block = new CallToolBlock(runtime, new FakeConnections());

        var paused = await block.Execute(BlockFakes.Context(new JsonObject { ["tool"] = "slow__crawl" }), TestContext.Current.CancellationToken);
        var bad = await block.Execute(BlockFakes.Context(new JsonObject { ["tool"] = "notes__add_note", ["arguments"] = new JsonObject { ["count"] = 1 } }), TestContext.Current.CancellationToken);

        Assert.Equal(SindriErrors.ToolkitPaused.Code, paused.Error.Code);
        Assert.Equal(SindriErrors.GraphBlockFailed.Code, bad.Error.Code);
        Assert.Empty(runtime.Calls);
    }

    [Fact]
    public async Task A_connections_mcp_tools_and_rest_preset_actions_are_listed_and_callable()
    {
        var connections = new FakeConnections();
        var block = new CallToolBlock(Runtime(), connections);

        var mcp = await block.ListConnectionTools("linear", TestContext.Current.CancellationToken);
        var rest = await block.ListConnectionTools("github", TestContext.Current.CancellationToken);
        var unknown = await block.ListConnectionTools("nope", TestContext.Current.CancellationToken);
        var toolkitRows = block.ListToolkitTools();
        var mcpCall = await block.Execute(BlockFakes.Context(new JsonObject { ["connection"] = "linear", ["tool"] = "search_issues", ["arguments"] = new JsonObject { ["q"] = "bug" } }), TestContext.Current.CancellationToken);
        var restCall = await block.Execute(BlockFakes.Context(new JsonObject { ["connection"] = "github", ["tool"] = "create_issue" }), TestContext.Current.CancellationToken);

        Assert.Equal([new CallableTool("search_issues", "linear", "Search issues.", "mcp")], mcp.Value);
        Assert.Equal([new CallableTool("create_issue", "github", "Open an issue.", "rest")], rest.Value);
        Assert.Equal(SindriErrors.ConnectionNotFound.Code, unknown.Error.Code);
        Assert.Equal(["notes__add_note"], toolkitRows.Select(static r => r.Tool), StringComparer.Ordinal);   // the paused toolkit is not offered
        Assert.Equal("linear", mcpCall.Value.Outputs["result"]!["via"]!.GetValue<string>());
        Assert.Equal("""{"q":"bug"}""", connections.Calls[0].Arguments);
        Assert.Equal(("github", "create_issue", "{}"), connections.Calls[1]);
        Assert.Equal("create_issue", restCall.Value.Outputs["result"]!["tool"]!.GetValue<string>());
    }
}
