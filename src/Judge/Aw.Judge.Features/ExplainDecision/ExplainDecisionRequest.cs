using Aw.Judge.Core.Ids;

namespace Aw.Judge.Features.ExplainDecision;

/// <summary>The ExplainDecision request ('judge why &lt;id&gt;').</summary>
/// <param name="DecisionId">The decision.</param>
public sealed record ExplainDecisionRequest(DecisionId DecisionId);
