using Aw.Judge.Core.Decisions;

namespace Aw.Judge.Features.UndoDecision;

/// <summary>The UndoDecision response.</summary>
/// <param name="Decision">The decision, now undone.</param>
public sealed record UndoDecisionResponse(DecisionRecord Decision);
