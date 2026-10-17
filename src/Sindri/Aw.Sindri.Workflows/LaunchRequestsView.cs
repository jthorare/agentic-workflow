using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The engine's read of the item's launches (UI spec E-19, API P10a) over the 'launches' view: the launch still 'requested' and inside its deadline, so a
/// start while one is in flight starts the instance taken over. A request past its deadline is not one (the launcher service fails it, and never opens it
/// late), and a key that is no item id has no launches. The caller waits for the view to reach the ledger head ('ReadsViews').
/// </summary>
/// <param name="launches">The launches view.</param>
/// <param name="clock">The clock the deadline is measured on.</param>
internal sealed class LaunchRequestsView(ILaunchViews launches, TimeProvider clock) : ILaunchRequests
{
    public async ValueTask<Result<LaunchId?>> Requested(ItemKey item, CancellationToken cancellationToken)
    {
        if (!ItemId.TryFrom(item.Value, out var id))
        {
            return (LaunchId?)null;
        }

        var rows = await launches.ForItem(id, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        var now = clock.GetUtcNow();
        return rows.Value.FirstOrDefault(row => LaunchRules.InFlight(row, now))?.Id;
    }
}
