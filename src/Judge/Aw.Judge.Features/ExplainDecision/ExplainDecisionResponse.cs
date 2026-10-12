using Aw.Judge.Core.Decisions;

namespace Aw.Judge.Features.ExplainDecision;

/// <summary>The ExplainDecision response.</summary>
/// <param name="Decision">The recorded decision.</param>
public sealed record ExplainDecisionResponse(DecisionRecord Decision);
