using System.Text;

namespace Aw.Connectors.Core.Slack;

/// <summary>
/// Where a Slack read stands: per channel, the newest ts seen (ConnectAI's per-channel watermark) and, while a channel is being walked in chunks, the pass in progress (<see cref="SlackPass"/>).
/// Text form 'C0123=1700000000.000100,C0456=…' sorted by channel id; a channel mid-pass reads 'C0123=watermark:top:below:threadsBelow' with the watermark, below and threadsBelow empty when unset.
/// A channel not in the map has not been read. Immutable.
/// </summary>
public sealed class SlackCursor
{
    private readonly Dictionary<string, Entry> _channels;

    private SlackCursor(Dictionary<string, Entry> channels)
    {
        _channels = channels;
    }

    /// <summary>The text a feed stores for a cursor that has read nothing (feeds refuse an empty cursor); <see cref="Parse"/> reads it as <see cref="Empty"/>.</summary>
    public const string NothingReadText = "-";

    /// <summary>A cursor that has read nothing.</summary>
    public static SlackCursor Empty { get; } = new([]);

    /// <summary>The watermark of <paramref name="channel"/>, or null when it has not been read whole.</summary>
    /// <param name="channel">The channel id.</param>
    /// <returns>The newest ts seen.</returns>
    public string? Newest(string channel) => _channels.TryGetValue(channel, out var entry) ? entry.Newest : null;

    /// <summary>The pass in progress in <paramref name="channel"/>, or null when none is.</summary>
    /// <param name="channel">The channel id.</param>
    /// <returns>The pass.</returns>
    public SlackPass? Pass(string channel) => _channels.TryGetValue(channel, out var entry) ? entry.Pass : null;

    /// <summary>The cursor with <paramref name="channel"/> moved to <paramref name="ts"/> (never backwards); a pass in progress there is over.</summary>
    /// <param name="channel">The channel id.</param>
    /// <param name="ts">The newest ts read in that channel.</param>
    /// <returns>The new cursor.</returns>
    public SlackCursor With(string channel, string ts)
    {
        var old = _channels.TryGetValue(channel, out var entry) ? entry.Newest : null;
        return Set(channel, new Entry(old is null ? ts : SlackTs.Later(old, ts), Pass: null));
    }

    /// <summary>The cursor with a pass in progress in <paramref name="channel"/>; its watermark stays where it was.</summary>
    /// <param name="channel">The channel id.</param>
    /// <param name="pass">The pass.</param>
    /// <returns>The new cursor.</returns>
    public SlackCursor WithPass(string channel, SlackPass pass)
    {
        ArgumentNullException.ThrowIfNull(pass);
        return Set(channel, new Entry(Newest(channel), pass));
    }

    /// <summary>The text form the Host stores.</summary>
    /// <returns>The text.</returns>
    public string ToText()
    {
        var text = new StringBuilder();
        foreach (var (channel, entry) in _channels.OrderBy(static e => e.Key, StringComparer.Ordinal))
        {
            _ = text.Append(text.Length == 0 ? string.Empty : ",").Append(channel).Append('=');
            _ = entry.Pass is { } pass
                ? text.Append(entry.Newest).Append(':').Append(pass.Top).Append(':').Append(pass.Below).Append(':').Append(pass.ThreadsBelow)
                : text.Append(entry.Newest);
        }

        return text.ToString();
    }

    /// <summary>Reads the text form (the old 'channel=ts' form too).</summary>
    /// <param name="text">What <see cref="ToText"/> made, or null for a first run.</param>
    /// <returns>The cursor, or null when the text is not a cursor (the caller starts over and says the cursor was reset).</returns>
    public static SlackCursor? Parse(string? text)
    {
        if (string.IsNullOrEmpty(text) || string.Equals(text, NothingReadText, StringComparison.Ordinal))
        {
            return Empty;
        }

        Dictionary<string, Entry> channels = new(StringComparer.Ordinal);
        foreach (var part in text.Split(','))
        {
            var pair = part.Split('=');
            if (pair is not [var channel, var value] || !SlackChannelIds.IsReadable(channel) || ParseEntry(value) is not { } entry)
            {
                return null;
            }

            channels[channel] = entry;
        }

        return new SlackCursor(channels);
    }

    private static Entry? ParseEntry(string value)
    {
        var fields = value.Split(':');
        return fields switch
        {
            [var ts] when SlackTs.TryParse(ts, out _) => new Entry(ts, Pass: null),
            [var newest, var top, var below, var threadsBelow] when Optional(newest) && SlackTs.TryParse(top, out _) && Optional(below) && Optional(threadsBelow)
                => new Entry(newest.Length == 0 ? null : newest, new SlackPass(top, below.Length == 0 ? null : below, threadsBelow.Length == 0 ? null : threadsBelow)),
            _ => null,
        };
    }

    private static bool Optional(string ts) => ts.Length == 0 || SlackTs.TryParse(ts, out _);

    private SlackCursor Set(string channel, Entry entry)
        => new(new Dictionary<string, Entry>(_channels, StringComparer.Ordinal) { [channel] = entry });

    private sealed record Entry(string? Newest, SlackPass? Pass);
}
