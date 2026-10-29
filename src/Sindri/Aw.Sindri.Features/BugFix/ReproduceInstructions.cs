using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.BugFix;

/// <summary>What 'step.reproduce.test-check' and 'step.reproduce.ui-check' are told (#1682): reproduce the original failure only, and keep the handoff block as read.</summary>
internal sealed class ReproduceInstructions : IAgentStepInstructions
{
    private static readonly BlockId[] _steps = [BlockId.From("step.reproduce.test-check"), BlockId.From("step.reproduce.ui-check")];

    /// <inheritdoc/>
    public bool IsFor(BlockId step) => Array.IndexOf(_steps, step) >= 0;

    /// <inheritdoc/>
    public IReadOnlyList<string> Lines => throw new NotSupportedException();
}
