using Aw.Kernel;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The item's launches as 'StartInstance' reads them (port; UI spec E-19, §6.4): the launch still 'requested' for an
/// item, so a start while one is in flight starts the instance taken over (<see cref="InstanceStart.OpenLaunch"/>).
/// The launches projection is API P10a's (UI E-1, 'GET /launches/{launchId}'); until it lands no launch is ever
/// requested. Engine P5 contract unit; unit (b) adds the adapter and the read in 'StartInstance'.
/// </summary>
public interface ILaunchRequests
{
    /// <summary>The launch still 'requested' for <paramref name="item"/>, once the launches view has reached the ledger head ('ReadsViews').</summary>
    /// <param name="item">The item.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The id of that launch; null when none is in flight. The runtime builds the open-request from it (actor 'service:engine').</returns>
    public ValueTask<Result<LaunchId?>> Requested(ItemKey item, CancellationToken cancellationToken);
}
