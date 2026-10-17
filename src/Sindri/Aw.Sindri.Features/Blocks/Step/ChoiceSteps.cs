using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>
/// The decision-tree Steps (DT spec §3.1, §3.3): descriptors only, desk-capable, with no IStep runtime until the block
/// runtime (DT-J), so each runs on the desk and a human picks. Their outcomes and inputs come from their parameters
/// (<see cref="BlockDescriptor.OutcomesFrom"/>, <see cref="BlockDescriptor.InputsFrom"/>), so the recipe's approved
/// text fixes the branches.
/// </summary>
internal static class ChoiceSteps
{
    private static readonly OutcomeName _uncertain = OutcomeName.From("uncertain");

    /// <summary>'step.judge.choose': one choice question; outcomes are the 'choices' names plus 'uncertain'; 'fact:decision' on every outcome, written by the engine.</summary>
    public static BlockDescriptor JudgeChoose { get; } = new(
        BlockId.From("step.judge.choose"), BlockKind.Step, Version: 1,
        Inputs: [],
        Outputs: [Decision],
        Outcomes: [_uncertain],
        Capability.Network,
        Parameters:
        [
            new ParameterSpec("prompt", ParameterType.Text, Required: true, IsCapabilityField: false),
            new ParameterSpec("choices", ParameterType.List, Required: true, IsCapabilityField: false),
            new ParameterSpec("facts", ParameterType.List, Required: true, IsCapabilityField: false),
            new ParameterSpec("threshold", ParameterType.Number, Required: false, IsCapabilityField: true),
            new ParameterSpec("providers", ParameterType.List, Required: false, IsCapabilityField: true),
        ],
        Rung: null,
        DeskCapable: true,
        OutcomesFrom: "choices",
        InputsFrom: "facts",
        Runtime: BlockRuntime.Block,   // human-only on the desk (DT spec §8.5) until a build registers its runtime (DT-J)
        MaxDuration: TimeSpan.FromMinutes(2));

    /// <summary>'step.desk.choose': the desk twin of a route state ('from'); outcomes are that state's choices, without 'uncertain'.</summary>
    public static BlockDescriptor DeskChoose { get; } = new(
        BlockId.From("step.desk.choose"), BlockKind.Step, Version: 1,
        Inputs: [],
        Outputs: [Decision],
        Outcomes: [],
        Capability.None,
        Parameters: [new ParameterSpec("from", ParameterType.Text, Required: true, IsCapabilityField: false)],
        Rung: null,
        DeskCapable: true,
        OutcomesFrom: "from.choices",
        InputsFrom: "from.facts",
        HumanOnly: true,
        Runtime: BlockRuntime.Desk);

    private static OutputSpec Decision => new(FactKey.From("fact:decision"), RequiredOn: [], Every: true, EngineFilled: true);
}
