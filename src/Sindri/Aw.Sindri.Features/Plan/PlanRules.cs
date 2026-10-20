using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.Plan;

/// <summary>The plan packet's rule ids and the stage each first runs at (line spec §6.3).</summary>
internal static class PlanRules
{
    internal const string Pkt1 = "PLN-PKT-001";
    internal const string Acc1 = "PLN-ACC-001";
    internal const string Size1 = "PLN-SIZE-001";
    internal const string Sig1 = "PLN-SIG-001";
    internal const string Stack1 = "PLN-STACK-001";
    internal const string Exec1 = "PLN-EXEC-001";
    internal const string Hold3 = "PLN-HOLD-003";

    /// <summary>Every rule in the order the checklist shows it.</summary>
    internal static IReadOnlyList<LineRule> All { get; } =
    [
        new(Pkt1, LineStage.Draft, PlanChecks.PacketValidates),
        new(Acc1, LineStage.Draft, PlanChecks.ItemsInOnePhase),
        new(Size1, LineStage.Draft, PlanChecks.PhasesWithinSize),
        new(Sig1, LineStage.Draft, PlanChecks.PhasesHaveSignals),
        new(Stack1, LineStage.Draft, PlanChecks.StacksStayShallow),
        new(Exec1, LineStage.Evidence, PlanChecks.PhasesExecutable),
        new(Hold3, LineStage.Evidence, PlanChecks.HoldoutSetsPlanned),
    ];
}
