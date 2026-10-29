using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.BugFix;

/// <summary>What 'step.implement.fix' is told (#1682): fix the instance and build the prevention the block decided, both proven.</summary>
internal sealed class ImplementFixInstructions : IAgentStepInstructions
{
    private static readonly BlockId _step = BlockId.From("step.implement.fix");

    /// <inheritdoc/>
    public bool IsFor(BlockId step) => step == _step;

    /// <inheritdoc/>
    public IReadOnlyList<string> Lines => throw new NotSupportedException();
}
