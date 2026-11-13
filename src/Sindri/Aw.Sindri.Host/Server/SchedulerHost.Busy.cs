using System.Collections.Concurrent;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Host.Server;

internal sealed partial class SchedulerHost
{
    // #3553: the services whose last run lost the command gate (SND-API-009), with the slot that run had. A busy gate is not a failed run: it isn't recorded
    // or logged as one (a recorded failure turns health Degraded), and the service runs again on the next beat for the same slot, ahead of its next slot.
    private readonly ConcurrentDictionary<ServiceName, DateTimeOffset> _busy = new();

    /// <summary>True when the service's slot came, or its last run found the command gate busy.</summary>
    private bool Due(ServiceSchedule schedule, DateTimeOffset now) => schedule.NextSlot <= now || _busy.ContainsKey(schedule.Name);

    /// <summary>The slot the run is for: the busy run's slot when it is a retry, so its 'service.ran' keeps that slot's key.</summary>
    private DateTimeOffset SlotOf(ServiceSchedule schedule) => _busy.TryGetValue(schedule.Name, out var slot) ? slot : schedule.NextSlot;

    /// <summary>A retry ahead of the slot keeps the slot; a run on its slot moves the series on.</summary>
    private static ServiceSchedule Next(ServiceSchedule schedule, DateTimeOffset now) => schedule.NextSlot <= now ? schedule.Advance(now) : schedule;

    private static bool IsBusy(Error error) => string.Equals(error.Code, SindriErrors.CommandBusy.Code, StringComparison.Ordinal);

    /// <summary>True when the run lost the command gate: it is queued for the next beat (logged once per busy stretch) and nothing is recorded.</summary>
    private bool Busy(ServiceName name, DateTimeOffset slot, Result<ScheduledResult> outcome)
    {
        if (outcome.IsError && IsBusy(outcome.Error))
        {
            if (_busy.TryAdd(name, slot))
            {
                LogBusy(log, name.Value, outcome.Error.Message);
            }

            return true;
        }

        _ = _busy.TryRemove(name, out _);
        return false;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "service '{Service}' found the command gate busy ({Detail}); it runs again on the next beat")]
    private static partial void LogBusy(ILogger logger, string service, string detail);
}
