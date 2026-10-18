using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine;

/// <summary>Why an instance is parked (spec §3.8), spelled as 'status', the evals' 'expect.park' and the needs-me list show it.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ParkReason>))]
public enum ParkReason
{
    /// <summary>A loop reached its cap (kind <see cref="ParkKind.Loop"/> or <see cref="ParkKind.Refusal"/>); answered by '--grant n'.</summary>
    [JsonStringEnumMemberName("rounds")]
    Rounds = 1,

    /// <summary>A loop round made no progress (kind loop or refusal); answered by '--grant n', or '--text' once.</summary>
    [JsonStringEnumMemberName("no-progress")]
    NoProgress = 2,

    /// <summary>A budget ran out, or a 'gate.budget.*' gate refused; answered by '--grant-tokens n'.</summary>
    [JsonStringEnumMemberName("budget")]
    Budget = 3,

    /// <summary>A refusal without a loop or a 'step.failed' (kind step), or a refused wait (kind wait); answered by '--text' or '--resume'.</summary>
    [JsonStringEnumMemberName("needs-answer")]
    NeedsAnswer = 4,

    /// <summary>A state ran past its timeout with 'then: park', or a retry timed out again; answered by '--resume'.</summary>
    [JsonStringEnumMemberName("timeout")]
    Timeout = 5,

    /// <summary>The recipe was revoked, a pinned child was revoked, a pinned block drifted, or dispatch refused an effect (kind tells which).</summary>
    [JsonStringEnumMemberName("spec-conflict")]
    SpecConflict = 6,

    /// <summary>A Step or resource with no runtime in this build (W5); answered by '--resume' in a build that has it.</summary>
    [JsonStringEnumMemberName("SND-WF-010")]
    NoRuntime = 7,

    /// <summary>A Step can't be confined to its policy (SND-LAUNCH-015) or its sandbox self-check failed (-016); answered by '--resume', which starts a new run (sandbox spec SB16).</summary>
    [JsonStringEnumMemberName("launch")]
    Launch = 8,
}
