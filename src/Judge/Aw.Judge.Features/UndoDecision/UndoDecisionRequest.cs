using Aw.Judge.Core.Ids;

namespace Aw.Judge.Features.UndoDecision;

/// <summary>The UndoDecision request ('judge undo &lt;id&gt;').</summary>
/// <param name="DecisionId">The decision.</param>
public sealed record UndoDecisionRequest(DecisionId DecisionId);
