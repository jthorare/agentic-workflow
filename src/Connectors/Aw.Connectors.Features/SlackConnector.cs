using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Connectors.Core;
using Aw.Connectors.Core.Slack;
using Aw.Connectors.Features.PullSlack;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Connectors.Features;

/// <summary>
/// The Slack source as an <see cref="IConnector"/> (owned index spec §5.2, §5.3): <see cref="Pull"/> runs the PullSlack slice over the profile's channels from a cursor that is the per-channel watermark
/// map; a rate limit that stopped the run before any channel moved is AW-CONNECTORS-009 with the reset time, one that stopped it after some progress returns that progress and the next pull reports the limit.
/// </summary>
internal sealed partial class SlackConnector(
    SlackSettings settings,
    ISlackReader reader,
    IService<PullSlackRequest, PullSlackResponse> pull,
    TimeSpan? window = null,
    TimeProvider? clock = null) : IConnector
{
    public ConnectorDescription Describe() => new("slack", "Slack");

    public async ValueTask<Result<ConnectorPage>> Pull(SourceCursor? after, int limit, DateTimeOffset? before, CancellationToken cancellationToken)
    {
        var parsed = SlackCursor.Parse(after?.Value);
        var cursor = parsed ?? SlackCursor.Empty;
        var result = await pull.Handle(new PullSlackRequest(settings.Channels, settings.Workspace, cursor, limit) { WindowStart = window is { } back ? (clock ?? TimeProvider.System).GetUtcNow() - back : null, Before = before }, cancellationToken).ConfigureAwait(false);
        if (result.IsError)
        {
            return result.Error;
        }

        var read = result.Value;
        return read.RateLimitedUntil is { } until && string.Equals(read.Next.ToText(), cursor.ToText(), StringComparison.Ordinal)
            ? Limited(until)
            : new ConnectorPage(read.Items, [], new SourceCursor(CursorText(read.Next)), CursorReset: parsed is null);
    }

    public async ValueTask<Result<IReadOnlyDictionary<string, string>>> Versions(IReadOnlyList<string> keys, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(keys);
        Dictionary<string, string> found = new(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            var match = Key.Match(key);
            var channel = match.Success ? settings.Channels.FirstOrDefault(c => string.Equals(c.Channel, match.Groups["channel"].Value, StringComparison.Ordinal)) : null;
            if (channel is null)
            {
                continue;
            }

            var version = await VersionOf(channel, match, cancellationToken).ConfigureAwait(false);
            if (version.IsError)
            {
                return version.Error;
            }

            if (version.Value is { } current)
            {
                found[key] = current;
            }
        }

        return found;
    }

    public async ValueTask<Result<Unit>> Probe(CancellationToken cancellationToken)
    {
        var probe = await reader.Probe(settings.Channels[0].Channel, cancellationToken).ConfigureAwait(false);
        if (probe.IsError)
        {
            return probe.Error;
        }

        return probe.Value is { } until ? Limited(until) : Unit.Value;
    }

    // why: a cursor with nothing read (an empty channel, or a backfill with nothing before its bound) has no text, and the feed refuses an empty cursor.
    private static string CursorText(SlackCursor cursor) => cursor.ToText() is { Length: > 0 } text ? text : SlackCursor.NothingReadText;

    private static Error Limited(DateTimeOffset until)
        => ConnectorsErrors.RateLimited.With($"Slack is rate limited until {until.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)}.");

    /// <summary>The version of one record, or null when the source no longer has it.</summary>
    private async ValueTask<Result<string?>> VersionOf(SlackChannelRef channel, Match key, CancellationToken cancellationToken)
    {
        if (key.Groups["day"].Success)
        {
            return await DayVersion(channel, key.Groups["day"].Value, cancellationToken).ConfigureAwait(false);
        }

        var thread = await reader.Replies(channel.Channel, key.Groups["ts"].Value, cancellationToken).ConfigureAwait(false);
        if (thread.IsError)
        {
            return thread.Error.Kind == ErrorKind.NotFound ? Results.Success<string?>(value: null) : thread.Error;
        }

        return thread.Value.RateLimitedUntil is { } until
            ? Limited(until)
            : Results.Success(thread.Value.Items.Count == 0 ? null : SlackRecords.VersionOf(SlackRecords.Newest(thread.Value.Items, DateTimeOffset.MinValue)));
    }

    private async ValueTask<Result<string?>> DayVersion(SlackChannelRef channel, string day, CancellationToken cancellationToken)
    {
        if (!DateTimeOffset.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var start))
        {
            return Results.Success<string?>(value: null);
        }

        var history = await reader.History(channel.Channel, SlackTs.Format(start), latest: null, cancellationToken).ConfigureAwait(false);
        if (history.IsError)
        {
            return history.Error;
        }

        if (history.Value.RateLimitedUntil is { } until)
        {
            return Limited(until);
        }

        var item = SlackRecords.Day(
            channel,
            settings.Workspace,
            day,
            [.. history.Value.Items.Where(m => !SlackRecords.IsNoise(m) && SlackRecords.IsUnthreaded(m) && string.Equals(SlackRecords.DayOf(m), day, StringComparison.Ordinal)).OrderBy(static m => m.Ts, StringComparer.Ordinal)]);
        return Results.Success(item?.Version);
    }

    [GeneratedRegex(@"^slack:(?<channel>[CG][A-Z0-9]{2,20})/(?:day/(?<day>\d{4}-\d{2}-\d{2})|(?<ts>\d{1,12}\.\d{6}))\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex Key { get; }
}
