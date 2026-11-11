using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Features.ListToolkits;

/// <summary>Reads the toolkit store.</summary>
/// <param name="store">The toolkit store.</param>
/// <param name="running">The toolkits that have a live process.</param>
internal sealed class ListToolkitsService(IToolkitStore store, IRunningToolkits running) : IService<ListToolkitsRequest, ListToolkitsResponse>
{
    public async ValueTask<Result<ListToolkitsResponse>> Handle(ListToolkitsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(running);
        var all = await store.List(cancellationToken).ConfigureAwait(false);
        if (all.IsError)
        {
            return all.Error;
        }

        var live = running.Toolkits().Where(static t => t.State is ToolkitState.Running or ToolkitState.Paused).ToDictionary(static t => t.Name, StringComparer.Ordinal);
        return new ListToolkitsResponse([.. all.Value.Select(t => Reconcile(t, live))]);
    }

    // The store can say 'running' or 'paused' for a process that is gone (a crash, a restart) or 'failed' for one that answers again; the live set is the truth for both.
    private static Toolkit Reconcile(Toolkit row, Dictionary<string, Toolkit> live) =>
        live.TryGetValue(row.Name, out var now)
            ? row with { State = now.State }
            : row.State is ToolkitState.Running or ToolkitState.Paused ? row with { State = ToolkitState.Built } : row;
}
