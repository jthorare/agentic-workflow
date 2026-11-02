using Aw.Judge.Core.Ids;

namespace Aw.Judge.Features.ExplainPromptSort;

/// <summary>The ExplainPromptSort request ('judge prompt-sort why &lt;decision-id&gt;').</summary>
/// <param name="Decision">The prompt-sort decision.</param>
public sealed record ExplainPromptSortRequest(DecisionId Decision);
