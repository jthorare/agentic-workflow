using Aw.Connectors.Core;
using Aw.Connectors.Core.Slack;
using Aw.Kernel;

namespace Aw.Connectors.Testing.Slack;

/// <summary>
/// An in-memory <see cref="ISlackReader"/>: serves what was added per channel the way Slack does (history newest first from an inclusive oldest, one page at a time for <see cref="HistoryPage"/>, a thread root first) and, like the real
/// client, refuses a channel that is not allowed before it reads anything (SND-INDEX-013). Contract-tested against the real client.
/// </summary>
public sealed class FakeSlackReader(params string[] allowedChannels) : ISlackReader
{
    private readonly SlackStore _store = new();
    private readonly HashSet<string> _allowed = new(allowedChannels ?? [], StringComparer.Ordinal);
    private readonly List<string> _reads = [];
    private readonly Dictionary<string, DateTimeOffset> _historyLimits = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _repliesLimits = new(StringComparer.Ordinal);
    private Error? _failure;
    private int? _repliesBudget;
    private DateTimeOffset? _repliesBudgetUntil;
    private int _repliesCalls;

    /// <summary>Gets or sets a callback run with the thread ts just before a thread read is served, so a test can add a message between a history scan and the thread read (a race); null runs nothing.</summary>
    public Action<string>? BeforeReplies { get; set; }

    /// <summary>The reads made, in order: 'history {channel} {oldest}', 'replies {channel} {ts}', 'probe {channel}'.</summary>
    public IReadOnlyList<string> Reads => _reads;

    /// <summary>Adds a message to a channel (a reply carries its root's ts as thread_ts).</summary>
    /// <param name="channel">The channel id.</param>
    /// <param name="message">The message.</param>
    /// <returns>This fake.</returns>
    public FakeSlackReader Add(string channel, SlackMessage message)
    {
        _store.Add(channel, message);
        return this;
    }

    /// <summary>Makes history reads stop on a rate limit until <paramref name="until"/>, with nothing read; null lifts the limit.</summary>
    /// <param name="until">When the quota comes back, or null for no limit.</param>
    /// <param name="channel">The one channel it applies to, or null for every channel.</param>
    /// <returns>This fake.</returns>
    public FakeSlackReader RateLimitHistory(DateTimeOffset? until, string? channel = null) => Limit(_historyLimits, until, channel);

    /// <summary>Makes thread reads stop on a rate limit until <paramref name="until"/>, with nothing read; null lifts the limit.</summary>
    /// <param name="until">When the quota comes back, or null for no limit.</param>
    /// <param name="channel">The one channel it applies to, or null for every channel.</param>
    /// <returns>This fake.</returns>
    public FakeSlackReader RateLimitReplies(DateTimeOffset? until, string? channel = null) => Limit(_repliesLimits, until, channel);

    /// <summary>Lets <paramref name="calls"/> thread reads succeed (counted from this call), then makes the next ones stop on a rate limit until <paramref name="until"/>, with nothing read; null lifts the limit.</summary>
    /// <param name="calls">How many thread reads still succeed.</param>
    /// <param name="until">When the quota comes back, or null to lift the limit.</param>
    /// <returns>This fake.</returns>
    public FakeSlackReader RateLimitRepliesAfter(int calls, DateTimeOffset? until)
    {
        _repliesBudget = until is null ? null : calls;
        _repliesBudgetUntil = until;
        _repliesCalls = 0;
        return this;
    }

    /// <summary>Makes every read fail with <paramref name="error"/>.</summary>
    /// <param name="error">The error.</param>
    /// <returns>This fake.</returns>
    public FakeSlackReader Fail(Error error)
    {
        _failure = error;
        return this;
    }

    /// <inheritdoc />
    public ValueTask<Result<SlackMessagePage>> History(string channel, string? oldest, string? latest, CancellationToken cancellationToken)
    {
        if (Refused(channel) is { } refused)
        {
            return ValueTask.FromResult<Result<SlackMessagePage>>(refused);
        }

        _reads.Add($"history {channel} {oldest}");
        return ValueTask.FromResult(Page(LimitOf(_historyLimits, channel), () => _store.Top(channel, oldest, latest)));
    }

    /// <inheritdoc />
    public ValueTask<Result<SlackMessagePage>> HistoryPage(string channel, string? oldest, string? latest, int limit, CancellationToken cancellationToken)
    {
        if (Refused(channel) is { } refused)
        {
            return ValueTask.FromResult<Result<SlackMessagePage>>(refused);
        }

        _reads.Add($"history {channel} {oldest}");
        var size = Math.Max(1, limit);
        return ValueTask.FromResult(_failure is not null
            ? (Result<SlackMessagePage>)_failure
            : LimitOf(_historyLimits, channel) is { } until
                ? new SlackMessagePage([], until)
                : PageOf(_store.Top(channel, oldest, latest), size));
    }

    /// <inheritdoc />
    public ValueTask<Result<SlackMessagePage>> Replies(string channel, string threadTs, CancellationToken cancellationToken)
    {
        if (Refused(channel) is { } refused)
        {
            return ValueTask.FromResult<Result<SlackMessagePage>>(refused);
        }

        if (_repliesBudget is { } budget && _repliesBudgetUntil is { } budgetUntil && _repliesCalls++ >= budget)
        {
            return ValueTask.FromResult<Result<SlackMessagePage>>(new SlackMessagePage([], budgetUntil));
        }

        _reads.Add($"replies {channel} {threadTs}");
        BeforeReplies?.Invoke(threadTs);
        return ValueTask.FromResult(Page(LimitOf(_repliesLimits, channel), () => _store.Thread(channel, threadTs)));
    }

    /// <inheritdoc />
    public ValueTask<Result<DateTimeOffset?>> Probe(string channel, CancellationToken cancellationToken)
    {
        if (Refused(channel) is { } refused)
        {
            return ValueTask.FromResult<Result<DateTimeOffset?>>(refused);
        }

        _reads.Add($"probe {channel}");
        return ValueTask.FromResult(_failure is not null ? Results.Failure<DateTimeOffset?>(_failure) : Results.Success(LimitOf(_historyLimits, channel)));
    }

    private FakeSlackReader Limit(Dictionary<string, DateTimeOffset> limits, DateTimeOffset? until, string? channel)
    {
        var key = channel ?? string.Empty;
        if (until is { } at)
        {
            limits[key] = at;
        }
        else
        {
            _ = limits.Remove(key);
        }

        return this;
    }

    private static DateTimeOffset? LimitOf(Dictionary<string, DateTimeOffset> limits, string channel)
        => limits.TryGetValue(channel, out var own) ? own : limits.TryGetValue(string.Empty, out var all) ? all : null;

    private Error? Refused(string channel)
        => !SlackChannelIds.IsReadable(channel) || !_allowed.Contains(channel)
            ? ConnectorsErrors.ChannelNotAllowed.With("The channel is not in the profile.")
            : null;

    private static Result<SlackMessagePage> PageOf(List<SlackMessage> all, int size)
        => new SlackMessagePage([.. all.Take(size)], RateLimitedUntil: null, HasMore: all.Count > size);

    private Result<SlackMessagePage> Page(DateTimeOffset? limit, Func<List<SlackMessage>> read)
        => _failure is not null
            ? _failure
            : limit is { } until ? new SlackMessagePage([], until) : new SlackMessagePage(read(), RateLimitedUntil: null);
}
