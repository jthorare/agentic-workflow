using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Pr;

/// <summary>What 'step.docs.update' is told (docs-sync spec §7.2): check its own docs commit with 'sindri docs check' before it reports.</summary>
internal sealed class DocsUpdateInstructions : IAgentStepInstructions
{
    /// <inheritdoc/>
    public bool IsFor(BlockId step) => throw new NotSupportedException();

    /// <inheritdoc/>
    public IReadOnlyList<string> Lines => throw new NotSupportedException();
}
