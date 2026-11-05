using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Workflow.Blocks;
using static Aw.Sindri.Features.Blocks.Step.StepSpec;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>The model check's Steps (model-check design §3, §4.1): the check itself, and the desk that asks the owner when it can't run.</summary>
internal static class ModelCheckSteps
{
    /// <summary>'step.model.check': runs TLC on the workspace's spec folders; 'pass' or 'skipped' with the bounds checked, any other outcome with findings.</summary>
    public static BlockDescriptor Check { get; } = BlockStep("step.model.check", Capability.Tools)
        .Takes(Text("specs"), Text("scope"), new ParameterSpec("witnesses", ParameterType.Flag, Required: false, IsCapabilityField: false))
        .Reads("fact:workspace", "fact:change", "fact:activePacket").Reports("pass", "skipped", "violation", "vacuous", "specError", "tooBig", "cantRun").Long(CommandStepLimits.ModelCheckMaxDuration)
        .Writes(On("fact:modelCheck", "pass", "skipped"), On("fact:findings", "violation", "vacuous", "specError", "tooBig", "cantRun"));

    /// <summary>
    /// 'step.model.desk': the build's call when the model check can't run, human-only: again or stop. RCP-OUTCOME-001 makes a
    /// recipe handle every outcome its Step reports, so scoping's again-or-skip is a desk of its own (M-5), not a third outcome here.
    /// </summary>
    public static BlockDescriptor Desk { get; } = HumanDeskStep("step.model.desk", Capability.None)
        .Reads("fact:findings").Reviewing(ReviewOf("fact:findings", DeskReviewKind.Text))
        .Reports("again", "stop").Writes();
}
