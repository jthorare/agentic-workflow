using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Tests.Graph.Blocks;

[Lane(TestLane.Unit)]
public sealed class PromptBlockTests
{
    private static JsonObject Config(string prompt = "Summarize {{s.facts}}") => new()
    {
        ["prompt"] = prompt,
        ["output"] = JsonNode.Parse("""{"type":"object","required":["summary","score"],"properties":{"summary":{"type":"string"},"score":{"type":"number"}}}"""),
    };

    [Fact]
    public async Task The_prompt_block_routes_through_the_workflowPrompt_route_and_validates_its_schema()
    {
        var good = new BlockFakes.ScriptedModelPort("""{"summary":"all quiet","score":2}""");
        var bad = new BlockFakes.ScriptedModelPort("""{"summary":"all quiet","score":"high"}""");
        var notJson = new BlockFakes.ScriptedModelPort("sure, here you go");

        var ok = await new PromptBlock(good).Execute(BlockFakes.Context(Config("Summarize the facts")), TestContext.Current.CancellationToken);
        var wrongType = await new PromptBlock(bad).Execute(BlockFakes.Context(Config()), TestContext.Current.CancellationToken);
        var unreadable = await new PromptBlock(notJson).Execute(BlockFakes.Context(Config()), TestContext.Current.CancellationToken);

        Assert.Equal(["Summarize the facts"], good.Prompts);
        Assert.True(ok.IsSuccess);
        Assert.Equal("all quiet", ok.Value.Outputs["summary"]?.GetValue<string>());
        Assert.Equal(2, ok.Value.Outputs["score"]?.GetValue<int>());
        Assert.True(wrongType.IsError);
        Assert.Contains("score", wrongType.Error.Message, StringComparison.Ordinal);
        Assert.True(unreadable.IsError);
        Assert.Contains("JSON", unreadable.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_answer_missing_a_required_field_fails_the_node_naming_the_field()
    {
        var port = new BlockFakes.ScriptedModelPort("""{"summary":"all quiet"}""");

        var result = await new PromptBlock(port).Execute(BlockFakes.Context(Config()), TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Contains("score", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_prompt_blocks_outputs_are_the_properties_of_its_output_schema()
    {
        var block = new PromptBlock(new BlockFakes.ScriptedModelPort("{}"));

        var outputs = block.OutputsFor(Config());

        Assert.Equal("string", outputs.Fields["summary"]);
        Assert.Equal("number", outputs.Fields["score"]);
        Assert.Equal("prompt", block.Type);
        Assert.False(block.IsTrigger);
    }
}
