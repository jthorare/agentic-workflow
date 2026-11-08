using Aw.Kernel;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The read side of the instances on Temporal (port; ARCH-CQRS-002): the queries take this, never <see cref="IInstanceRuntime"/>,
/// which starts and signals. A read changes nothing.
/// </summary>
public interface IInstanceReader
{
    /// <summary>The 'GetInstance' Query.</summary>
    /// <param name="address">The instance.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The instance, or SND-WF-001.</returns>
    public ValueTask<Result<InstanceView>> Get(InstanceAddress address, CancellationToken cancellationToken);
}
