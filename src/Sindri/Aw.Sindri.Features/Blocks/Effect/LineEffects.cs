using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>
/// The line's report effect (line spec BD7, LN-WF1): its runtime is LineReportEffect: it records the report in the phase's run area and replies
/// 'line.phaseChanged' with 'fact:phaseEvent' for the line.
/// </summary>
internal static class LineEffects
{
    /// <summary>'effect.line.report': tells the line a phase has 'shipped' or 'merged'; 'line' is the line context fact.</summary>
    public static BlockDescriptor Report { get; } = new(
        BlockId.From("effect.line.report"), BlockKind.Effect, Version: 1, Inputs: [], Outputs: [new OutputSpec(FactKey.From("fact:phaseEvent"), [])], Outcomes: [],
        Capability.Effects,
        Parameters:
        [
            new ParameterSpec("status", ParameterType.Text, Required: true, IsCapabilityField: false),
            new ParameterSpec("line", ParameterType.Text, Required: true, IsCapabilityField: false),
        ],
        Rung: null,
        DeskCapable: false);
}
