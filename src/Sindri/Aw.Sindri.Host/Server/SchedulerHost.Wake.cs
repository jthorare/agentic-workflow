using Aw.Sindri.Core.Connections.Polling;

namespace Aw.Sindri.Host.Server;

internal sealed partial class SchedulerHost
{
    // why: after a sleep the slots of these services are in the past or far ahead; they run now (workflow-schedule fires its latest missed tick, connection-refresh renews tokens that expired asleep, workflow-poll catches up)
    private static readonly string[] _wakeServices = ["workflow-poll", "workflow-schedule", "connection-refresh"];

    private static readonly TimeSpan _tick = TimeSpan.FromSeconds(1);   // the scheduler's own loop

    private WakeDetector? _wakes;

    // A gap over 3 x 30 s (the poll service's tick) between the wall and the monotonic clock is a sleep: the poll service reads every cursor forward on its next run (#2258, PR 11) and the wake services run now.
    private void AfterSleep()
    {
        _wakes ??= new WakeDetector(clock, TimeSpan.FromSeconds(30));
        if (!_wakes.Observe())
        {
            return;
        }

        inputs.PollWake?.Request();
        var now = clock.GetUtcNow();
        _schedules = [.. _schedules.Select(s => _wakeServices.Contains(s.Name.Value, StringComparer.Ordinal) && s.NextSlot > now ? s with { NextSlot = now } : s)];
        inputs.Live.Publish(_schedules);
    }
}
