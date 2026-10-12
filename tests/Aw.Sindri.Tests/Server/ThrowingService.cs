using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Tests.Server;

/// <summary>A background service named 'boom' whose every run throws, as a bug would.</summary>
internal sealed class ThrowingService : IScheduledService
{
    public ServiceName Identity() => ServiceName.From("boom");

    public ValueTask<Result<Unit>> Run(ScheduledRun run, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("boom failed");
}
