using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Tests.Server;

/// <summary>The scheduler's pauses when no test pauses anything: the empty set.</summary>
internal sealed class NoPauses : IServicePauses
{
    public ValueTask<Result<IReadOnlySet<ServiceName>>> Paused(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success<IReadOnlySet<ServiceName>>(new HashSet<ServiceName>()));

    public ValueTask<Result<DateTimeOffset?>> ResumedAt(ServiceName service, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success<DateTimeOffset?>(value: null));
}
