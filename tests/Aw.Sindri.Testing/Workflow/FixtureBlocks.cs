using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// Test-only descriptors for the validate-only fixtures (spec §12): 'workflow.bugfix' (the bug-fix worker's phases,
/// docs/csharp/bugfix.md, as 'step.bugfix.*' blocks) and 'workflow.direction' (the direction check, core design §6.1).
/// None ships in the binary (W24).
/// </summary>
public static class FixtureBlocks
{
    /// <summary>The bug-fix worker's Steps and gates.</summary>
    public static IReadOnlyList<BlockDescriptor> Bugfix { get; } =
    [
        Step("step.bugfix.investigate", ["fact:ticket"], ["diagnosed", "unfixed"], ("fact:rootCause", "diagnosed")),
        Step("step.bugfix.reproduce", ["fact:ticket", "fact:rootCause"], ["oneArea", "manyAreas", "notReproduced"],
            ("fact:check", "oneArea"), ("fact:check", "manyAreas")),
        Step("step.bugfix.fix-single", ["fact:rootCause", "fact:check"], ["committed"], ("fact:candidate", "committed")),
        Step("step.bugfix.fix-split", ["fact:rootCause", "fact:check"], ["committed"], ("fact:candidate", "committed")),
        Step("step.bugfix.evaluate", ["fact:check", "fact:candidate"], ["resolved", "retrySingle", "retrySplit", "giveUp"],
            ("fact:verdict", "resolved"), ("fact:findings", "retrySingle"), ("fact:findings", "retrySplit")),
        Gate("gate.bugfix.frozen-check", Rung.Deterministic),
        Gate("gate.bugfix.resolution", Rung.JudgeQuestion),
    ];

    /// <summary>The direction check's Steps and verdict gate.</summary>
    public static IReadOnlyList<BlockDescriptor> Direction { get; } =
    [
        Step("step.direction.challenge", ["fact:position"], ["challenged", "agreed"], ("fact:challenge", "challenged")),
        Step("step.direction.reply", ["fact:position", "fact:challenge"], ["replied"], ("fact:reply", "replied")),
        Step("step.direction.verdict", ["fact:position"], ["proceed", "revise", "another"], ("fact:verdict", "proceed"), ("fact:verdict", "revise")),
        Gate("gate.direction.verdict", Rung.JudgeQuestion),
    ];

    /// <summary>The direction check's events: the proposer's position and the delivery ack, both spooled by the hooks.</summary>
    public static IReadOnlyList<EventDescriptor> DirectionEvents { get; } =
    [
        new(EventKind.From("direction.position"), "hook.spool", EventRoute.Item, [OutcomeName.From("submitted")], [FactKey.From("fact:position")]),
        new(EventKind.From("direction.delivered"), "hook.spool", EventRoute.Item, [OutcomeName.From("acknowledged")], []),
    ];

    private static BlockDescriptor Step(string id, string[] inputs, string[] outcomes, params (string Fact, string On)[] outputs) => new(
        BlockId.From(id), BlockKind.Step, Version: 1,
        Inputs: [.. inputs.Select(FactKey.From)],
        Outputs: [.. outputs.GroupBy(o => o.Fact, StringComparer.Ordinal)
            .Select(g => new OutputSpec(FactKey.From(g.Key), [.. g.Select(o => OutcomeName.From(o.On))]))],
        Outcomes: [.. outcomes.Select(OutcomeName.From)],
        Capability.None, Parameters: [], Rung: null, DeskCapable: true);

    private static BlockDescriptor Gate(string id, Rung rung) => new(
        BlockId.From(id), BlockKind.Gate, Version: 1, Inputs: [], Outputs: [], Outcomes: [], Capability.None, Parameters: [], rung, DeskCapable: false);
}
