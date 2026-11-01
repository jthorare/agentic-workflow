using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Features.GetNeedsYou;

/// <summary>
/// Finds the open instances that wait on the owner at a human-verb wait: a wait state of the instance's pinned recipe with a 'command.&lt;verb&gt;' exit whose
/// effect acts with a human's authority. The instances are listed once, each instance and each recipe read once; a read that fails leaves that instance off.
/// </summary>
/// <param name="instances">Lists the open instances.</param>
/// <param name="directory">Resolves an instance's address.</param>
/// <param name="reader">The instances' views.</param>
/// <param name="registry">Reads an instance's pinned recipe back by its hash.</param>
/// <param name="catalog">Says which effects act with a human's authority.</param>
internal sealed class OwnerStopReader(IWorkflowInstances instances, IInstanceDirectory directory, IInstanceReader reader, RecipeRegistryReader registry, BlockCatalog catalog)
{
    /// <summary>The instances at a human-verb wait that nothing else holds (no open question, run or desk task, not ended).</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The candidates; empty when the instances can't be listed.</returns>
    public ValueTask<IReadOnlyList<OwnerStopCandidate>> Candidates(CancellationToken cancellationToken)
    {
        _ = (instances, directory, reader, registry, catalog, cancellationToken);
        throw new NotSupportedException();
    }
}
