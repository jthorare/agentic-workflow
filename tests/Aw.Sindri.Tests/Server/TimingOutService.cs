using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Tests.Server;

/// <summary>A background service named 'slow' whose every run times out on its own (an HttpClient timeout throws this).</summary>
internal sealed class TimingOutService : IScheduledService
{
    public ServiceName Identity() => ServiceName.From("slow");

    public ValueTask<Result<Unit>> Run(ScheduledRun run, CancellationToken cancellationToken) =>
        throw new TaskCanceledException("the request timed out");
}
