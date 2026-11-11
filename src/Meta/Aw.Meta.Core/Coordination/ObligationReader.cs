using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Coordination;

/// <summary>Reads everything the open obligations are derived from and evaluates them.</summary>
/// <param name="messages">Inbound and outbound messages.</param>
/// <param name="lanes">The lane map.</param>
/// <param name="owner">The owner's answers.</param>
/// <param name="gates">The latest merge.</param>
/// <param name="values">The watch heartbeat.</param>
public sealed class ObligationReader(IMessageStore messages, ILaneStore lanes, IOwnerStore owner, IGateStore gates, IKeyValueStore values)
{
    private const int DefaultIntervalSeconds = 120;

    /// <summary>Reads the facts and evaluates the open set.</summary>
    /// <param name="metaLane">The meta lane's name; it is never sent broadcasts.</param>
    /// <param name="now">The current time.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <param name="metaContext">The meta lane's measured context; null when not measured.</param>
    /// <param name="metaWaiter">True when a detached recycle waiter is recorded for the meta lane.</param>
    /// <returns>The open obligations.</returns>
    public async ValueTask<Result<IReadOnlyList<Obligation>>> Open(string metaLane, DateTimeOffset now, CancellationToken cancellationToken, LaneContext? metaContext = null, bool metaWaiter = false)
    {
        var inbound = await messages.Messages(cancellationToken).ConfigureAwait(false);
        var sent = await messages.Sent(cancellationToken).ConfigureAwait(false);
        var map = await lanes.Current(cancellationToken).ConfigureAwait(false);
        var answers = await owner.Answers(cancellationToken).ConfigureAwait(false);
        var merge = await gates.LatestMerge(cancellationToken).ConfigureAwait(false);
        var beat = await values.Get(KvKeys.WatchHeartbeat, cancellationToken).ConfigureAwait(false);
        var interval = await values.Get(KvKeys.WatchInterval, cancellationToken).ConfigureAwait(false);
        var failed = new Error?[] { Failure(inbound), Failure(sent), Failure(map), Failure(answers), Failure(merge), Failure(beat), Failure(interval) }.FirstOrDefault(e => e is not null);
        if (failed is not null)
        {
            return failed;
        }

        return Results.Success(ObligationEvaluator.Evaluate(new ObligationFacts
        {
            Messages = inbound.Value,
            Outbound = sent.Value,
            Lanes = map.Value,
            MetaLane = metaLane,
            LatestMerge = merge.Value,
            Answers = [.. answers.Value.Where(a => !string.Equals(a.AskedBy, metaLane, StringComparison.OrdinalIgnoreCase))],   // the meta's own questions need no relay
            Heartbeat = Beat(beat.Value),
            WatchInterval = Interval(interval.Value),
            MetaContext = metaContext,
            MetaWaiter = metaWaiter,
            Now = now,
        }));
    }

    private static Error? Failure<T>(Result<T> result) => result.IsError ? result.Error : null;

    // The watch's own interval, so a slow watch isn't called stale; the default when none was stored.
    private static TimeSpan Interval(string? text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds > 0 ? TimeSpan.FromSeconds(seconds) : TimeSpan.FromSeconds(DefaultIntervalSeconds);

    private static DateTimeOffset? Beat(string? text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at) ? at : null;
}
