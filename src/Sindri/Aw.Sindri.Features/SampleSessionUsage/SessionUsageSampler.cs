using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launches;

namespace Aw.Sindri.Features.SampleSessionUsage;

/// <summary>
/// Appends 'session.usage' (SCR E-28, private) for the live launches that know their agent session id, from the usage reader the agent's entry names. The key is
/// 'session.usage:&lt;launch&gt;:&lt;n&gt;' with n counting the sampling intervals since the launch started, so a restart cannot reuse a number. A launch whose totals
/// did not change since the last sample writes nothing.
/// </summary>
/// <param name="liveness">The launches this process tracks.</param>
/// <param name="views">The 'launches' view: the session id, the agent and the item.</param>
/// <param name="usage">The usage readers.</param>
/// <param name="samples">The last totals.</param>
/// <param name="events">Appends the sample.</param>
/// <param name="clock">The time.</param>
internal sealed class SessionUsageSampler(LaunchLiveness liveness, ILaunchViews views, ILaunchUsage usage, UsageSamples samples, LaunchSessionEvents events, TimeProvider clock) : ISessionUsageSampler
{
    public async ValueTask<int> Sample(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var tracked = liveness.Active();
        samples.Keep(tracked.ToHashSet());
        var appended = 0;
        foreach (var launch in tracked)
        {
            var read = await views.Get(launch, cancellationToken).ConfigureAwait(false);
            if (read.IsError || read.Value is not { Live: true, ResumableId: { } session } row)
            {
                continue;
            }

            var tokens = await usage.OfSession(row.Target, session, cancellationToken).ConfigureAwait(false);
            if (tokens is null)
            {
                continue;
            }

            var input = tokens.Sum(static t => t.Input + t.CacheRead + t.CacheWrite);
            var output = tokens.Sum(static t => t.Output);
            if ((input, output) == (0, 0) || !samples.Changed(launch, input, output))
            {
                continue;
            }

            var number = (int)Math.Floor(Math.Max(0, (now - (row.OutcomeAt ?? row.RequestedAt)) / LaunchRules.UsageEvery)) + 1;
            if ((await events.Usage(row.Item.Value, new SessionUsage(launch, number, input, output), cancellationToken).ConfigureAwait(false)).IsSuccess)
            {
                appended++;
            }
        }

        return appended;
    }
}
