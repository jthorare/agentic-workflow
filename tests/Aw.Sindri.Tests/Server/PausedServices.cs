using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Tests.Server;

/// <summary>Pauses as a human set them: the named services are paused.</summary>
/// <param name="names">The paused services.</param>
internal sealed class PausedServices(params ServiceName[] names) : IServicePauses
{
    public ValueTask<Result<IReadOnlySet<ServiceName>>> Paused(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success<IReadOnlySet<ServiceName>>(new HashSet<ServiceName>(names)));
}
