using Aw.Judge.Core.Ids;

namespace Aw.Judge.Features.ApproveDecision;

/// <summary>The ApproveDecision request ('judge approve &lt;id&gt;').</summary>
/// <param name="DecisionId">The decision.</param>
public sealed record ApproveDecisionRequest(DecisionId DecisionId);
