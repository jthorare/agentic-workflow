using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>
/// The Step contract's three Steps (spec §12): descriptors only, desk-capable, so with no IStep runtime registered they
/// run on the desk runtime (W5, W20) until the agent runtime (P8) runs them.
/// </summary>
internal static class DeskSteps
{
    /// <summary>'step.produce': outcome 'done', with the required output 'fact:output'.</summary>
    public static BlockDescriptor Produce { get; } = Desk(
        "step.produce", inputs: [], outcomes: ["done"], outputs: [Output("fact:output", requiredOn: "done")]);

    /// <summary>'step.checks': outcomes 'passed' and 'failed', with 'fact:findings' required on 'failed'.</summary>
    public static BlockDescriptor Checks { get; } = Desk(
        "step.checks", inputs: ["fact:output"], outcomes: ["passed", "failed"], outputs: [Output("fact:findings", requiredOn: "failed")]);

    /// <summary>'step.verify': outcomes 'accepted' and 'rejected', with 'fact:findings' required on 'rejected'.</summary>
    public static BlockDescriptor Verify { get; } = Desk(
        "step.verify", inputs: ["fact:output"], outcomes: ["accepted", "rejected"], outputs: [Output("fact:findings", requiredOn: "rejected")]);

    private static BlockDescriptor Desk(string id, string[] inputs, string[] outcomes, OutputSpec[] outputs) => new(
        BlockId.From(id), BlockKind.Step, Version: 1,
        Inputs: [.. inputs.Select(FactKey.From)],
        Outputs: outputs,
        Outcomes: [.. outcomes.Select(OutcomeName.From)],
        Capability.None, Parameters: [], Rung: null, DeskCapable: true);

    private static OutputSpec Output(string fact, string requiredOn) => new(FactKey.From(fact), [OutcomeName.From(requiredOn)]);
}
