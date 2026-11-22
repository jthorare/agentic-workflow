using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Validation;

namespace Aw.Sindri.Tests.Graph.Blocks;

/// <summary>
/// #3072: the Workflows editor's Add step opens the sheet pre-filled with the block's example config, and Save sends it untouched as one add_node. So the example of EVERY block in the menu must pass the
/// required-settings check the server runs on each edit (GRAPH-CONFIG-001), here the real GraphValidator over the real standard block registry. A row that fails names the block and the field.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class BlockExamplesAcceptedTests
{
    private static readonly GraphBlockRegistry _registry = GraphBlockRegistry.Standard(new BlockFakes.RecordingNotifyPort(), new BlockFakes.ScriptedModelPort("{}"));

    public static TheoryData<string> Kinds() => [.. _registry.Types.Keys.Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(Kinds))]
    public void The_example_of_every_menu_block_passes_the_required_settings_check(string type)
    {
        var block = _registry.Types[type];
        var config = JsonNode.Parse(block.Example) as JsonObject;
        Assert.True(config is not null, $"the '{type}' block's example is not a JSON object: {block.Example}");
        var spec = GraphFixtures.Spec([new GraphNode(NodeId.From("added_1"), type, Title: null, config)], []);

        var result = GraphFixtures.Validator(blocks: _registry.Types).Validate(spec, GraphMode.Draft);

        Assert.True(
            result.Findings.All(static f => f.Code != "GRAPH-CONFIG-001"),
            $"'{type}': " + string.Join("; ", result.Findings.Where(static f => f.Code == "GRAPH-CONFIG-001").Select(static f => f.ToString())));
    }
}
