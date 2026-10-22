using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>What the interpreter does with its state timer after a change (engine spec §5.5). Engine P5 contract unit.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TimerActionKind>))]
public enum TimerActionKind
{
    /// <summary>Nothing: the same entry and run with no suspension change, so the armed timer (or none) stays.</summary>
    [JsonStringEnumMemberName("keep")]
    Keep = 1,

    /// <summary>Arm the state's full timeout in a new scope: a new entry, a new run, or an answer that ended a park ('rearm').</summary>
    [JsonStringEnumMemberName("arm")]
    Arm = 2,

    /// <summary>Cancel the scope with no remainder: a park or escalation, an end, or an entry into a state with no timeout.</summary>
    [JsonStringEnumMemberName("stop")]
    Stop = 3,

    /// <summary>A takeover: cancel the scope, keep what was left on the suspension, and arm 'takeoverIdle' (2 h).</summary>
    [JsonStringEnumMemberName("pause")]
    Pause = 4,

    /// <summary>A handback: cancel 'takeoverIdle' and re-arm the paused timer with what was left (nothing when it covered a park).</summary>
    [JsonStringEnumMemberName("resume")]
    Resume = 5,
}
