using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>One block as 'workflow_blocks' lists it: read straight from <see cref="IGraphBlockType"/>, plus the example spec from its doc page.</summary>
public sealed class GraphBlockEntry
{
    /// <summary>Makes an entry.</summary>
    /// <param name="block">The block type, the one source of every field but the example spec.</param>
    /// <param name="exampleSpec">A whole workflow spec using the block, from its doc page, or null when the page has none.</param>
    public GraphBlockEntry(IGraphBlockType block, string? exampleSpec)
    {
        ArgumentNullException.ThrowIfNull(block);
        Type = block.Type;
        Title = block.Title;
        Group = block.Group;
        IsTrigger = block.IsTrigger;
        Config = block.Config;
        ExampleConfig = block.Example;
        Outputs = block.OutputsFor(ParseConfig(block.Example));
        ExampleSpec = exampleSpec;
    }

    /// <summary>The type name a node's 'type' uses.</summary>
    public string Type { get; }

    /// <summary>The picker label.</summary>
    public string Title { get; }

    /// <summary>The picker group.</summary>
    public BlockGroup Group { get; }

    /// <summary>True for a block that starts a run.</summary>
    public bool IsTrigger { get; }

    /// <summary>The fields the config takes.</summary>
    public BlockSchema Config { get; }

    /// <summary>One valid node config, as JSON text.</summary>
    public string ExampleConfig { get; }

    /// <summary>What the block hands on when configured as in <see cref="ExampleConfig"/>.</summary>
    public BlockOutputs Outputs { get; }

    /// <summary>A whole workflow spec that uses the block, or null.</summary>
    public string? ExampleSpec { get; }

    private static JsonObject ParseConfig(string json)
    {
        try
        {
            return JsonNode.Parse(json) as JsonObject ?? new JsonObject();
        }
        catch (System.Text.Json.JsonException)
        {
            return new JsonObject();
        }
    }
}
