using Aw.Judge.Core.Decisions;

namespace Aw.Judge.Features.ApproveDecision;

/// <summary>The ApproveDecision response.</summary>
/// <param name="Decision">The decision, with its approved time.</param>
public sealed record ApproveDecisionResponse(DecisionRecord Decision);
