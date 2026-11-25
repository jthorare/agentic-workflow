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

    /// <summary>The label the workflow picker shows, such as 'Send notification'. A spec keeps the type name, so a new label never breaks a saved workflow.</summary>
    public string Title { get; }

    /// <summary>The picker group the block sits in.</summary>
    public BlockGroup Group { get; }

    /// <summary>True for a block that starts a run (schedule, connection_event, inbox, workflow_call): a workflow has exactly one.</summary>
    public bool IsTrigger { get; }

    /// <summary>The fields the config takes.</summary>
    public BlockSchema Config { get; }

    /// <summary>One valid node config, as JSON text, that documentation shows.</summary>
    public string Example { get; }

    /// <summary>What a node of this type hands on, given its config (a 'run_agent' declares its fields in the config).</summary>
    /// <param name="config">The node's config.</param>
    /// <returns>The fields and branches.</returns>
    public BlockOutputs OutputsFor(JsonObject config);

    /// <summary>Problems in the config that the field types in <see cref="Config"/> can't express (a 'prompt' block's 'output' schema). None by default.</summary>
    /// <param name="config">The node's config.</param>
    /// <returns>One problem per field: the field's name and what to change.</returns>
    public IReadOnlyList<(string Field, string Problem)> CheckConfig(JsonObject config) => [];

    /// <summary>The line the canvas shows on a node of this type, built from its config. The block's title by default.</summary>
    /// <param name="config">The node's config.</param>
    /// <returns>The line and, when something required is missing, what.</returns>
    public BlockSummary Summarize(JsonObject config) => new(Title);
}
