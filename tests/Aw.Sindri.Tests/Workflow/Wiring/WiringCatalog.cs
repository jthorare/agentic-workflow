using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Tests.Workflow.Wiring;

/// <summary>
/// The catalog the wiring and block rule tests validate against: the §12 blocks and events as their descriptors
/// declare them, plus test-only blocks for what §12 doesn't ship (a judge-question gate with parameters, an evidence
/// provider and a Step input).
/// </summary>
internal static class WiringCatalog
{
    private static IReadOnlyList<EventDescriptor> Events { get; } =
    [
        new(EventKind.From("item.routed"), "router.stub", EventRoute.Item, [OutcomeName.From("startable"), OutcomeName.From("notStartable")],
            [FactKey.From("fact:stepSpec")]),
        new(EventKind.From("claim.created"), "system:dispatcher", EventRoute.Causation, [OutcomeName.From("created"), OutcomeName.From("claimExists")], []),
        new(EventKind.From("notify.delivered"), "system:dispatcher", EventRoute.Causation, [OutcomeName.From("delivered")], []),
    ];

    public static BlockCatalog Shipped { get; } = new(
        [
            Step("step.produce", [], ["done"], ("fact:output", "done")),
            Step("step.checks", ["fact:output"], ["passed", "failed"], ("fact:findings", "failed")),
            Step("step.verify", ["fact:output"], ["accepted", "rejected"], ("fact:findings", "rejected")),
            Gate("gate.budget.available", Rung.Deterministic, [], []),
            Gate("gate.route.startable", Rung.Deterministic, [], []),
            Effect("effect.claim.ref", ["fact:item"]),
            Effect("effect.notify.desk", []),
        ],
        Events);

    public static BlockCatalog WithTestBlocks { get; } = new(
        [
            .. Shipped.Blocks,
            Gate(
                "gate.review.judge",
                Rung.JudgeQuestion,
                ["fact:output"],
                [
                    new ParameterSpec("question", ParameterType.Text, Required: true, IsCapabilityField: false),
                    new ParameterSpec("rounds", ParameterType.Number, Required: false, IsCapabilityField: false),
                    new ParameterSpec("strict", ParameterType.Flag, Required: false, IsCapabilityField: false),
                    new ParameterSpec("tags", ParameterType.List, Required: false, IsCapabilityField: false),
                ]),
            Step("step.test.approve", [], ["approved"], ("fact:approval", "approved")) with { HumanOnly = true },
            Effect("effect.test.merge", []) with { HumanTrigger = true },
            new BlockDescriptor(BlockId.From("evidence.checks"), BlockKind.EvidenceProvider, Version: 1, Inputs: [], Outputs: [], Outcomes: [],
                Capability.None, Parameters: [], Rung: null, DeskCapable: false),
            new BlockDescriptor(BlockId.From("input.context.retrieve"), BlockKind.StepInput, Version: 1, Inputs: [FactKey.From("fact:spec")],
                Outputs: [], Outcomes: [], Capability.None, Parameters: [], Rung: null, DeskCapable: false),
        ],
        Events);

    private static BlockDescriptor Step(string id, string[] inputs, string[] outcomes, (string Fact, string On) output) => new(
        BlockId.From(id), BlockKind.Step, Version: 1,
        Inputs: [.. inputs.Select(FactKey.From)],
        Outputs: [new OutputSpec(FactKey.From(output.Fact), [OutcomeName.From(output.On)])],
        Outcomes: [.. outcomes.Select(OutcomeName.From)],
        Capability.None, Parameters: [], Rung: null, DeskCapable: true);

    private static BlockDescriptor Gate(string id, Rung rung, string[] inputs, IReadOnlyList<ParameterSpec> parameters) => new(
        BlockId.From(id), BlockKind.Gate, Version: 1, Inputs: [.. inputs.Select(FactKey.From)], Outputs: [], Outcomes: [], Capability.None,
        parameters, rung, DeskCapable: false);

    private static BlockDescriptor Effect(string id, string[] inputs) => new(
        BlockId.From(id), BlockKind.Effect, Version: 1, Inputs: [.. inputs.Select(FactKey.From)], Outputs: [], Outcomes: [], Capability.Effects,
        Parameters: [], Rung: null, DeskCapable: false);
}
