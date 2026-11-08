using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// The in-memory <see cref="ILaunchRequests"/> (UI spec E-19): the launches a test marks requested, read by
/// their id, as the open-request the engine sends is built from it. Passes <see cref="LaunchRequestsContract"/>.
/// </summary>
public sealed class InMemoryLaunchRequests : ILaunchRequests
{
    private readonly Dictionary<ItemKey, LaunchId> _requested = [];

    /// <summary>Marks <paramref name="launch"/> requested for <paramref name="item"/>, as API P10a's 'OpenItem' records it.</summary>
    /// <param name="item">The item.</param>
    /// <param name="launch">The launch id.</param>
    public void Request(ItemKey item, LaunchId launch) => _requested[item] = launch;

    /// <inheritdoc/>
    public ValueTask<Result<LaunchId?>> Requested(ItemKey item, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<LaunchId?>>(_requested.TryGetValue(item, out var launch) ? (LaunchId?)launch : null);
}
