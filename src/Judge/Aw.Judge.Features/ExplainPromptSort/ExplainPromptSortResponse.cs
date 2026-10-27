using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.PromptSort;

namespace Aw.Judge.Features.ExplainPromptSort;

/// <summary>The ExplainPromptSort response.</summary>
/// <param name="Decision">The decision with its details.</param>
/// <param name="Sort">Its axes and run.</param>
public sealed record ExplainPromptSortResponse(DecisionRecord Decision, PromptSortRecord Sort);
