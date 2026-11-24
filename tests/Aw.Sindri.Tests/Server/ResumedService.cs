using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Tests.Server;

/// <summary>Pauses after a human resumed one service: nothing is paused, and the named service was last resumed at the given time.</summary>
/// <param name="name">The resumed service.</param>
/// <param name="at">When it was resumed.</param>
internal sealed class ResumedService(ServiceName name, DateTimeOffset at) : IServicePauses
{
    public ValueTask<Result<IReadOnlySet<ServiceName>>> Paused(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success<IReadOnlySet<ServiceName>>(new HashSet<ServiceName>()));

    public ValueTask<Result<DateTimeOffset?>> ResumedAt(ServiceName service, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success<DateTimeOffset?>(service.Equals(name) ? at : null));
}
