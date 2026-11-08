using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Memory;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.ExpireMemory;

/// <summary>The 'memory-expire' built-in service: once an hour it deletes every fact whose expiry has passed. It runs without a profile entry.</summary>
/// <param name="store">The memories.</param>
/// <param name="time">The clock.</param>
internal sealed class ExpireMemoryService(IMemoryStore store, TimeProvider time) : IScheduledService
{
    /// <summary>What the service does, as GET /sindri/services says it.</summary>
    public const string Description = "Deletes memory facts whose expiry has passed.";

    private readonly IMemoryStore _store = store;
    private readonly TimeProvider _time = time;

    /// <summary>Gets the service's name in the schedule ('memory-expire').</summary>
    internal static ServiceName ServiceId { get; } = ServiceName.From("memory-expire");

    /// <summary>Gets the hourly backstop the scheduler uses without a profile entry.</summary>
    internal static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(TimeSpan.FromHours(1)), Description);

    /// <inheritdoc/>
    public ServiceName Identity() => ServiceId;

    /// <inheritdoc/>
    public async ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        var deleted = await _store.DeleteExpired(_time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return deleted.IsError ? deleted.Error : deleted.Value > 0 ? ScheduledResult.Worked : ScheduledResult.Quiet;
    }
}
