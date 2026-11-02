using Aw.BugFix.Core.Ids;

namespace Aw.BugFix.Core.Model;

/// <summary>The one judge resolution-check verdict recorded for a candidate.</summary>
/// <param name="Decision">The decision.</param>
/// <param name="DecisionId">judge's stored decision id; null when judge escalated without one.</param>
/// <param name="ReasonCode">judge's reason code.</param>
/// <param name="InputSha256">SHA-256 of the input file the helper wrote and asked about (judge-&lt;candidate&gt;.json).</param>
/// <param name="Reasons">judge's reasons, so a later session sees why; null in a state file written before they were kept (not recorded, different from none).</param>
public sealed record JudgeVerdict(JudgeDecision Decision, JudgeDecisionId? DecisionId, string ReasonCode, string InputSha256, IReadOnlyList<string>? Reasons = null);
