using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
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
            appended += await SampleLaunch(launch, now, requireLive: true, cancellationToken).ConfigureAwait(false);
        }

        return appended;
    }

    public async ValueTask<int> Flush(LaunchId launch, CancellationToken cancellationToken) =>
        await SampleLaunch(launch, clock.GetUtcNow(), requireLive: false, cancellationToken).ConfigureAwait(false);

    /// <summary>The one sample the tick and the flush share (a flush needs no live session: the agent that completed and exited still has its final totals): 1 when a 'session.usage' was appended, 0 when there was nothing new to say.</summary>
    private async ValueTask<int> SampleLaunch(LaunchId launch, DateTimeOffset now, bool requireLive, CancellationToken cancellationToken)
    {
        using var turn = await samples.Turn(launch, cancellationToken).ConfigureAwait(false);   // one at a time per launch: the check, the number, the append and the record are one step
        var read = await views.Get(launch, cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value is not { ResumableId: { } session } row || (requireLive && !row.Live))
        {
            return 0;
        }

        return await Append(launch, row, session, now, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<int> Append(LaunchId launch, LaunchRow row, SessionId session, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var tokens = await usage.OfSession(row.Target, session, cancellationToken).ConfigureAwait(false);
        if (tokens is null)
        {
            return 0;
        }

        var (input, output) = ModelTokens.Totals(tokens);
        if ((input, output) == (0, 0) || !samples.IsNew(launch, input, output))
        {
            return 0;
        }

        var number = samples.Next(launch, (int)Math.Floor(Math.Max(0, (now - (row.OutcomeAt ?? row.RequestedAt)) / LaunchRules.UsageEvery)) + 1);
        var sample = new SessionUsage(launch, number, input, output, row.Target, ModelTokens.Dominant(tokens), ModelTokens.CacheReads(tokens) is > 0 and var cached ? cached : null);
        if ((await events.Usage(row.Item.Value, sample, cancellationToken).ConfigureAwait(false)).IsError)
        {
            return 0;
        }

        samples.Record(launch, input, output, number);   // only a sample that is in the ledger counts as seen
        return 1;
    }
}
