using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>
/// A workflow ledger row as the recipe stats read it: plain values only, mapped from the ledger by the feature's source, so the counting
/// (in Core) depends on no payload type. Rows arrive in ledger order.
/// </summary>
/// <param name="Address">The instance address.</param>
/// <param name="At">When the ledger recorded the row.</param>
/// <param name="From">The state the row left (empty for a start).</param>
/// <param name="To">The state it entered, or an engine-owned one ('parked', 'escalated', 'taken-over', 'cancelled').</param>
/// <param name="RecipeId">The recipe's id, on the row that started the instance; null on any other row.</param>
/// <param name="RecipeHash">The recipe's hash, on the row that started the instance; null on any other row.</param>
/// <param name="HasDetail">Whether the row carries the post-state delta; when it does not, <paramref name="Suspension"/> says nothing about the suspension.</param>
/// <param name="Suspension">The kind of the suspension after the row, null when there is none.</param>
/// <param name="ParkReason">A park's reason after the row.</param>
/// <param name="Escalation">An escalation's kind after the row.</param>
/// <param name="Answered">Whether the row's trigger was a human's answer.</param>
/// <param name="Outcome">The terminal outcome, on the row that ended the instance.</param>
/// <param name="TokensSpent">The instance's token spend after the row, when the row says.</param>
/// <param name="Gates">The gates' answers on the row.</param>
public sealed record StatEvent(
    string Address,
    DateTimeOffset At,
    string From,
    string To,
    RecipeId? RecipeId,
    string? RecipeHash,
    bool HasDetail,
    SuspensionKind? Suspension,
    ParkReason? ParkReason,
    EscalationKind? Escalation,
    bool Answered,
    string? Outcome,
    int? TokensSpent,
    IReadOnlyList<StatGate> Gates);
