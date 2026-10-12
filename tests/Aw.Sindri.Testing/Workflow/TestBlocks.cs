using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>Descriptors for test blocks: a desk-capable Step with one outcome and a deterministic gate.</summary>
public static class TestBlocks
{
    /// <summary>A Step descriptor: outcome 'done', required output 'fact:output', desk-capable.</summary>
    /// <param name="id">The Step's id.</param>
    /// <returns>The descriptor.</returns>
    public static BlockDescriptor Step(string id) => new(
        BlockId.From(id), BlockKind.Step, Version: 1, Inputs: [],
        Outputs: [new OutputSpec(FactKey.From("fact:output"), [OutcomeName.From("done")])],
        Outcomes: [OutcomeName.From("done")], Capability.None, Parameters: [], Rung: null, DeskCapable: true);

    /// <summary>A deterministic gate descriptor.</summary>
    /// <param name="id">The gate's id.</param>
    /// <returns>The descriptor.</returns>
    public static BlockDescriptor Gate(string id) => new(
        BlockId.From(id), BlockKind.Gate, Version: 1, Inputs: [], Outputs: [], Outcomes: [], Capability.None, Parameters: [],
        Rung.Deterministic, DeskCapable: false);

    /// <summary>An item-routed event descriptor with the given outcomes.</summary>
    /// <param name="kind">The event's kind.</param>
    /// <param name="outcomes">Its outcomes.</param>
    /// <returns>The descriptor.</returns>
    public static EventDescriptor Event(string kind, params string[] outcomes) => new(
        EventKind.From(kind), "router.stub", EventRoute.Item, [.. outcomes.Select(OutcomeName.From)], []);
}
