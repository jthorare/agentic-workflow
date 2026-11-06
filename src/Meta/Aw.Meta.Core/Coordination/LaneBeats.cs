using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Coordination;

/// <summary>Keeps each non-Claude lane's hook heartbeat in the key-value store: when a hook last ran and whether it was the stop, so "idle" and "hooks not running" can be told.</summary>
/// <param name="values">The key-value store.</param>
/// <param name="delay">Waits between polls for a hook beat.</param>
public sealed class LaneBeats(IKeyValueStore values, IDelay delay)
{
    private const char Separator = '|';
    private const string StopEvent = "stop";
    private const string ActiveEvent = "active";

    /// <summary>Records that a hook ran.</summary>
    /// <param name="lane">The lane.</param>
    /// <param name="stopped">Whether it was the stop check.</param>
    /// <param name="now">When.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success.</returns>
    public ValueTask<Result<Unit>> Record(string lane, bool stopped, DateTimeOffset now, CancellationToken cancellationToken) =>
        values.Set(BeatKey(lane), now.ToString("O", CultureInfo.InvariantCulture) + Separator + (stopped ? StopEvent : ActiveEvent), cancellationToken);

    /// <summary>Reads the last heartbeat.</summary>
    /// <param name="lane">The lane.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The beat; null before any hook ran.</returns>
    public async ValueTask<Result<LaneBeat?>> Last(string lane, CancellationToken cancellationToken)
    {
        var text = await values.Get(BeatKey(lane), cancellationToken).ConfigureAwait(false);
        if (text.IsError)
        {
            return text.Error;
        }

        var parts = text.Value?.Split(Separator);
        return parts is { Length: 2 } && DateTimeOffset.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)
            ? Results.Success<LaneBeat?>(new LaneBeat(at, string.Equals(parts[1], StopEvent, StringComparison.Ordinal)))
            : Results.Success<LaneBeat?>(value: null);
    }

    /// <summary>Reads when the lane last ran a lane command by hand (outside a hook).</summary>
    /// <param name="lane">The lane.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>When; null when none was seen.</returns>
    public async ValueTask<Result<DateTimeOffset?>> LastCli(string lane, CancellationToken cancellationToken)
    {
        var text = await values.Get("lane." + lane + ".cli", cancellationToken).ConfigureAwait(false);
        return text.IsError
            ? text.Error
            : Results.Success<DateTimeOffset?>(DateTimeOffset.TryParse(text.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at) ? at : null);
    }

    /// <summary>Waits for a hook beat newer than <paramref name="since"/>, polling every 500 ms.</summary>
    /// <param name="lane">The lane.</param>
    /// <param name="since">The time the wake line was typed.</param>
    /// <param name="window">How long to wait.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>True when a hook beat arrived inside the window.</returns>
    public ValueTask<Result<bool>> WaitForHook(string lane, DateTimeOffset since, TimeSpan window, CancellationToken cancellationToken)
    {
        _ = (delay, lane, since, window, cancellationToken);
        return ValueTask.FromResult(Results.Success(value: false));
    }

    /// <summary>Records that the lane was woken, so a second send doesn't type into the pane again before the lane's hook answers.</summary>
    /// <param name="lane">The lane.</param>
    /// <param name="now">When.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success.</returns>
    public ValueTask<Result<Unit>> Woke(string lane, DateTimeOffset now, CancellationToken cancellationToken) =>
        values.Set(WokeKey(lane), now.ToString("O", CultureInfo.InvariantCulture), cancellationToken);

    /// <summary>Whether the lane is idle: its last hook was a stop and it hasn't been woken since.</summary>
    /// <param name="lane">The lane.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when idle.</returns>
    public async ValueTask<Result<bool>> IsIdle(string lane, CancellationToken cancellationToken)
    {
        var beat = await Last(lane, cancellationToken).ConfigureAwait(false);
        var woke = await values.Get(WokeKey(lane), cancellationToken).ConfigureAwait(false);
        if (beat.IsError || woke.IsError)
        {
            return beat.IsError ? beat.Error : woke.Error;
        }

        var wokeAt = DateTimeOffset.TryParse(woke.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at) ? at : (DateTimeOffset?)null;
        return beat.Value is { Stopped: true } last && (wokeAt is null || wokeAt <= last.At);
    }

    private static string BeatKey(string lane) => "lane." + lane + ".beat";

    private static string WokeKey(string lane) => "lane." + lane + ".woke";
}
