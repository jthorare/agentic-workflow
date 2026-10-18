using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine;

/// <summary>Which row of spec §3.8 a park is, where its reason has more than one: it decides the text and which answers end it.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ParkKind>))]
public enum ParkKind
{
    /// <summary>Rounds or no-progress, on the transition back into a looped state: the grant performs that withheld transition.</summary>
    [JsonStringEnumMemberName("loop")]
    Loop = 1,

    /// <summary>Rounds or no-progress, on a gate refusal at the cap: the grant re-runs the Step (grants never skip a gate).</summary>
    [JsonStringEnumMemberName("refusal")]
    Refusal = 2,

    /// <summary>Needs-answer or budget, from a Step trigger.</summary>
    [JsonStringEnumMemberName("step")]
    Step = 3,

    /// <summary>Needs-answer or budget, from a wait: a gate refused the transition out of it.</summary>
    [JsonStringEnumMemberName("wait")]
    Wait = 4,

    /// <summary>Spec-conflict: this instance's own recipe was revoked. Only cancel ends it.</summary>
    [JsonStringEnumMemberName("revoked")]
    Revoked = 5,

    /// <summary>Spec-conflict: the child hash this recipe pins was revoked; the parent's re-approval resumes it.</summary>
    [JsonStringEnumMemberName("child-revoked")]
    ChildRevoked = 6,

    /// <summary>Spec-conflict: a pinned block changed in this build; the drift approval resumes it.</summary>
    [JsonStringEnumMemberName("drift")]
    Drift = 7,

    /// <summary>Spec-conflict: dispatch refused an effect with SND-WF-011 (not allowlisted); '--resume' re-emits it.</summary>
    [JsonStringEnumMemberName("dispatch")]
    Dispatch = 8,

    /// <summary>Launch: the Step or its agent can't be confined to the policy (SND-LAUNCH-015).</summary>
    [JsonStringEnumMemberName("unconfinable")]
    Unconfinable = 9,

    /// <summary>Launch: the sandbox self-check failed (SND-LAUNCH-016).</summary>
    [JsonStringEnumMemberName("self-check")]
    SelfCheck = 10,
}
