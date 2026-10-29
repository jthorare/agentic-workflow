#pragma warning disable MA0182, MA0041, CA1822 // red commit: the stub is registered in the commit that implements it
using Aw.Context.Core.Models;
using Aw.Judge.Core.Ports;
using Aw.Kernel;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Host.Models;

/// <summary>The context engine's model port with routing (model routing spec §4): which harnesses a call tries and which model each runs, the per-job limit memory, the pause when every harness is limited, and the 'context.model.called' event.</summary>
internal sealed class RoutedModelPort : IModelPort
{
    /// <summary>Creates the port.</summary>
    /// <param name="catalog">Finds the provider lineup (resolved on the first request).</param>
    /// <param name="models">'context.models' of the approved profile.</param>
    /// <param name="jobs">The job the call runs under, and the handle that parks it.</param>
    /// <param name="log">Where the call's event is written.</param>
    /// <param name="clock">Times the tries.</param>
    public RoutedModelPort(Func<IProviderCatalog> catalog, IApprovedModels models, IJobContextAccessor jobs, IModelCallLog log, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(jobs);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(clock);
    }

    /// <summary>How many jobs the limited set remembers a harness for.</summary>
    internal int JobsRemembered => throw new NotSupportedException();

    public ValueTask<Result<ModelAnswer>> Ask(ModelRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
