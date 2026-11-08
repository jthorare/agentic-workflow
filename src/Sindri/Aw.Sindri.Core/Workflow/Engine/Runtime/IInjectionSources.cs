using Aw.Kernel;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>Where the open run's input sources come from (a query on the instance; the text is not among them).</summary>
public interface IInjectionSources
{
    /// <summary>The sources of the instance's open run.</summary>
    /// <param name="address">The instance address.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The sources; an instance that is not there is SND-WF-001.</returns>
    public ValueTask<Result<InjectionSource>> Read(InstanceAddress address, CancellationToken cancellationToken);
}
