using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// What a block type is to the graph: its name, whether it starts a run, its config schema, what it hands on and one example.
/// The single source for the validator and for the block listing ('workflow_blocks', PR 8).
/// </summary>
public interface IGraphBlockType
{
    /// <summary>The name a node's 'type' uses, such as 'run_agent'.</summary>
    public string Type { get; }

    /// <summary>True for a block that starts a run (schedule, poll, inbox, workflow_call): a workflow has exactly one.</summary>
    public bool IsTrigger { get; }

    /// <summary>The fields the config takes.</summary>
    public BlockSchema Config { get; }

    /// <summary>One valid node config, as JSON text, that documentation shows.</summary>
    public string Example { get; }

    /// <summary>What a node of this type hands on, given its config (a 'run_agent' declares its fields in the config).</summary>
    /// <param name="config">The node's config.</param>
    /// <returns>The fields and branches.</returns>
    public BlockOutputs OutputsFor(JsonObject config);
}
