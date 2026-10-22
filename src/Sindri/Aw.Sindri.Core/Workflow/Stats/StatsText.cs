using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>The reason words the stats group parks and escalations by.</summary>
internal static class StatsText
{
    public static string Park(ParkReason? reason) => reason switch
    {
        ParkReason.Rounds => "rounds",
        ParkReason.NoProgress => "no-progress",
        ParkReason.Budget => "budget",
        ParkReason.NeedsAnswer => "needs-answer",
        ParkReason.Timeout => "timeout",
        ParkReason.SpecConflict => "spec-conflict",
        ParkReason.NoRuntime => "SND-WF-010",
        _ => "unknown",
    };

    public static string Escalation(EscalationKind? kind) => kind switch
    {
        EscalationKind.Gate => "gate",
        EscalationKind.Timeout => "timeout",
        EscalationKind.Child => "child",
        _ => "unknown",
    };
}
