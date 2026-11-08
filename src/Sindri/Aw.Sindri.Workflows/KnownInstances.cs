using System.Collections.Concurrent;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The instance ids this process has seen, with their addresses (engine spec §6.3): an id is a digest of its address, so
/// it can't be read back; the runtime remembers every instance it starts or queries (so the second hop of 'status' by id finds it), and the
/// directory finds an id here before it scans visibility, which can lag a start by a moment. It is a read cache, not instance state.
/// </summary>
internal sealed class KnownInstances
{
    private readonly ConcurrentDictionary<WorkflowInstanceId, InstanceAddress> _byId = new();

    /// <summary>Remembers the instance at <paramref name="address"/>.</summary>
    /// <param name="address">Its address.</param>
    public void Remember(InstanceAddress address) => _byId[WorkflowInstanceId.Of(address)] = address;

    /// <summary>The address of <paramref name="id"/>, when this process has seen it.</summary>
    /// <param name="id">The id.</param>
    /// <returns>The address, or null.</returns>
    public InstanceAddress? Find(WorkflowInstanceId id) => _byId.TryGetValue(id, out var address) ? address : null;
}
