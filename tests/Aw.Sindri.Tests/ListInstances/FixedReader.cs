using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Tests.ListInstances;

internal sealed class FixedReader(IReadOnlyList<InstanceView> views) : IInstanceReader
{
    /// <summary>The address of every Get, in order.</summary>
    public List<string> Reads { get; } = [];

    public ValueTask<Result<InstanceView>> Get(InstanceAddress address, CancellationToken cancellationToken)
    {
        Reads.Add(address.Value);
        return ValueTask.FromResult<Result<InstanceView>>(views.First(v => v.Address == address));
    }
}
