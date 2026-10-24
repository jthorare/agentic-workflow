using Aw.BugFix.Core.Ids;

namespace Aw.BugFix.Core.Model;

/// <summary>judge resolution-check's answer, as the judge port reports it.</summary>
/// <param name="Decision">The decision; <see cref="JudgeDecision.Escalated"/> when judge hands it to a human.</param>
/// <param name="DecisionId">The stored decision's id; required for every decision but an escalation.</param>
/// <param name="ReasonCode">judge's reason code.</param>
/// <param name="Reasons">judge's reasons, for the implementer brief on a retry.</param>
/// <param name="InputSha256">The judge's input digest (16 hex) of the input it decided on; the helper refuses an answer about any other input.</param>
public sealed record JudgeAnswer(JudgeDecision Decision, JudgeDecisionId? DecisionId, string ReasonCode, IReadOnlyList<string> Reasons, string InputSha256);
