using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Review;

/// <summary>What 'step.review.fix' is told (#1682): fix the findings; when one is a real bug, ask the prevention question first.</summary>
internal sealed class ReviewFixInstructions : IAgentStepInstructions
{
    private static readonly BlockId _step = BlockId.From("step.review.fix");

    /// <inheritdoc/>
    public bool IsFor(BlockId step) => step == _step;

    /// <inheritdoc/>
    public IReadOnlyList<string> Lines => throw new NotSupportedException();
}
