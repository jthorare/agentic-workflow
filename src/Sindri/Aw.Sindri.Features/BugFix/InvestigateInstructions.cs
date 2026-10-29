using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.BugFix;

/// <summary>What 'step.rootcause.investigate' is told (#1682): confirm the cause, ask the prevention question, and hand the block on in fact:rootCause.</summary>
internal sealed class InvestigateInstructions : IAgentStepInstructions
{
    private static readonly BlockId _step = BlockId.From("step.rootcause.investigate");

    /// <inheritdoc/>
    public bool IsFor(BlockId step) => step == _step;

    /// <inheritdoc/>
    public IReadOnlyList<string> Lines => BugClassPrevention.SharedLines;   // stub: red first (#1682)
}
