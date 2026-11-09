using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Coordination;

/// <summary>Says whether a beating watch is consuming the meta's mail (#2370).</summary>
public static class ConsumerState
{
    private const int DefaultIntervalSeconds = 120;

    /// <summary>Reads the watch heartbeat and describes who consumes mail.</summary>
    /// <param name="values">Holds the heartbeat and interval.</param>
    /// <param name="waiting">How many messages wait for a reply; null when not known (the line then makes no claim about waiting mail).</param>
    /// <param name="now">The current time.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The line, and whether mail waits with no active consumer.</returns>
    public static async ValueTask<Result<(string Line, bool Missing)>> Read(IKeyValueStore values, int? waiting, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(values);
        var beat = await values.Get(KvKeys.WatchHeartbeat, cancellationToken).ConfigureAwait(false);
        var interval = await values.Get(KvKeys.WatchInterval, cancellationToken).ConfigureAwait(false);
        if (beat.IsError || interval.IsError)
        {
            return beat.IsError ? beat.Error : interval.Error;
        }

        var every = int.TryParse(interval.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds > 0 ? seconds : DefaultIntervalSeconds;
        DateTimeOffset? last = DateTimeOffset.TryParse(beat.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at) ? at : null;
        return Describe(last, TimeSpan.FromSeconds(every), waiting, now);
    }

    private static (string Line, bool Missing) Describe(DateTimeOffset? last, TimeSpan every, int? waiting, DateTimeOffset now)
    {
        var when = last is { } l ? l.ToString("HH:mm", CultureInfo.InvariantCulture) : "never";
        if (last is { } fresh && now - fresh <= every)
        {
            return ($"active (watch last beat {when})", false);
        }

        return waiting switch
        {
            > 0 => (string.Create(CultureInfo.InvariantCulture, $"no active coordinator consumer: watch last beat {when}, {waiting} waiting (aw-meta watch --until-mail)"), true),
            0 => ($"idle (watch last beat {when}, nothing waiting)", false),
            _ => ($"no watch beating (last beat {when}; run aw-meta watch --until-mail if mail waits)", false),
        };
    }
}
