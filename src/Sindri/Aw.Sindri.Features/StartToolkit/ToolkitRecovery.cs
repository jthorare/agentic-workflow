using Aw.Kernel;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Features.StartToolkit;

/// <summary>Puts the toolkits a previous serve left 'running' or 'paused' back to 'built': the new serve has no processes.</summary>
/// <param name="store">The toolkit store.</param>
public sealed class ToolkitRecovery(IToolkitStore store)
{
    /// <summary>Resets every such row.</summary>
    /// <param name="cancellationToken">Cancels the pass.</param>
    /// <returns>How many rows it changed, or the store's error.</returns>
    public async ValueTask<Result<int>> Run(CancellationToken cancellationToken)
    {
        var all = await store.List(cancellationToken).ConfigureAwait(false);
        if (all.IsError)
        {
            return all.Error;
        }

        var changed = 0;
        foreach (var toolkit in all.Value.Where(static t => t.State is ToolkitState.Running or ToolkitState.Paused))
        {
            var saved = await store.Save(toolkit with { State = ToolkitState.Built }, cancellationToken).ConfigureAwait(false);
            if (saved.IsError)
            {
                return saved.Error;
            }

            changed++;
        }

        return changed;
    }
}
