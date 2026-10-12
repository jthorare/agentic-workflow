using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>A block's kind (spec §2), named by its id's prefix (BlockKinds.Of).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<BlockKind>))]
public enum BlockKind
{
    /// <summary>'gate.…': checks a transition (IGate).</summary>
    [JsonStringEnumMemberName("gate")]
    Gate = 1,

    /// <summary>'evidence.…': collects evidence (IEvidenceProvider).</summary>
    [JsonStringEnumMemberName("evidenceProvider")]
    EvidenceProvider = 2,

    /// <summary>'step.…': the work of an active state (IStep).</summary>
    [JsonStringEnumMemberName("step")]
    Step = 3,

    /// <summary>'effect.…': a command a transition emits through the outbox (IEffectKind).</summary>
    [JsonStringEnumMemberName("effect")]
    Effect = 4,

    /// <summary>'input.…': fills sections of a Step's injection plan before it starts (IStepInput).</summary>
    [JsonStringEnumMemberName("stepInput")]
    StepInput = 5,
}
