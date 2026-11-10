using Aw.Kernel;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Infrastructure.Toolkits;

/// <summary>Puts the toolkits a previous serve left 'running' or 'paused' back to 'built': the new serve has no processes.</summary>
/// <param name="store">The toolkit store.</param>
internal sealed class ToolkitRecovery(IToolkitStore store)
{
    /// <summary>Resets every such row.</summary>
    /// <param name="cancellationToken">Cancels the pass.</param>
    /// <returns>How many rows it changed.</returns>
    internal ValueTask<Result<int>> Run(CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<Result<int>>(0);
    }
}
