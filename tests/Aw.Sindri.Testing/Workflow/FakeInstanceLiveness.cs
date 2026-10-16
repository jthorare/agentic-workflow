using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>The instances a test says are live; every other one has ended or never started.</summary>
public sealed class FakeInstanceLiveness : IInstanceLiveness
{
    /// <summary>The live instances.</summary>
    public ISet<InstanceAddress> Live { get; } = new HashSet<InstanceAddress>();

    /// <inheritdoc/>
    public ValueTask<Result<bool>> IsLive(InstanceAddress address, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<bool>>(Live.Contains(address));
}
