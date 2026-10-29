using Aw.Connectors.Core;
using Aw.Connectors.Core.Slack;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Connectors.Features.PullSlack;

/// <summary>
/// Reads the listed channels in bounded chunks and keeps its place inside a channel. A channel is walked newest to oldest in a pass: one chunk reads history pages (one request each, paged by the
/// exclusive 'latest' bound) until a message from an older day appears or history runs out. Every day of the chunk but its oldest (unless history ran out) is complete: its day record is emitted from
/// history alone, then its thread roots are read, newest first, at most <see cref="RepliesPerPull"/> replies reads per pull. The cursor moves after every chunk and every thread, so a rate limit, the
/// feed's time cap or a cancellation keeps what was read and the next pull resumes at the next unread thread or chunk. When history runs out the channel's watermark becomes the newest root the pass saw.
/// A pull loops chunks across the channels until it has records and (the limit is reached or the replies budget is spent), a rate limit stops it, or every channel is done; it returns an empty page only
/// when nothing is left. A channel with nothing newer than its watermark is skipped (the watermark's day is still read once to find that out).
/// Before that skip, a channel with a watermark and no pass in progress gets a thread refresh: history from <see cref="ThreadRefresh.RefreshDays"/> days before the watermark's day up to the watermark is read, and every thread root whose
/// latest reply is newer than the channel's replies mark (<see cref="SlackCursor.RepliesMark"/>) is read again, oldest latest reply first; each emitted thread moves the mark, so a rate limit or the replies budget resumes
/// at the next one, and nothing changed means nothing emitted (the incremental run still ends on an empty page). A thread whose root is older than the refresh window is not re-read by a pull.
/// </summary>
internal sealed class PullSlackService(ISlackReader reader) : IService<PullSlackRequest, PullSlackResponse>
{
    /// <summary>The most thread reads (conversations.replies) one pull makes once it has records.</summary>
    internal const int RepliesPerPull = 20;

    internal const int PageSize = 200;
    internal const int MaxPagesPerChunk = 500;

    public async ValueTask<Result<PullSlackResponse>> Handle(PullSlackRequest request, CancellationToken cancellationToken)
    {
        var run = new PullRun(request.After);
        try
        {
            foreach (var channel in request.Channels)
            {
                var step = await ReadChannel(request, channel, run, cancellationToken).ConfigureAwait(false);
                if (step.IsError)
                {
                    return step.Error;
                }

                if (run.Limited is not null || run.Enough(request.Limit))
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested && run.Items.Count > 0)
        {
            // why: the feed's time cap cancels the read; what was read is committed with the cursor it moved, and the next pull resumes there.
        }

        // why: a pull that read nothing must not hand back a moved cursor with an empty page (the consumer reads an empty page as 'finished').
        return run.Limited is { } until
            ? new PullSlackResponse(run.Items, run.Items.Count == 0 ? request.After : run.Next, until)
            : new PullSlackResponse(run.Items, run.Next, RateLimitedUntil: null);
    }

    private static string? Oldest(PullSlackRequest request, SlackChannelRef channel)
        => request.After.Newest(channel.Channel) is { } watermark
            ? SlackTs.DayStart(watermark)
            : request.WindowStart is { } window ? SlackTs.Format(window) : null;

    private static string? Latest(PullSlackRequest request) => request.Before is { } end ? SlackTs.Format(end) : null;

    private async ValueTask<Result<bool>> ReadChannel(PullSlackRequest request, SlackChannelRef channel, PullRun run, CancellationToken cancellationToken)
    {
        if (run.Next.Newest(channel.Channel) is { } watermark && run.Next.Pass(channel.Channel) is null)
        {
            var start = run.Items.Count;
            var refresh = await new ThreadRefresh(reader).Run(request, channel, watermark, run, cancellationToken).ConfigureAwait(false);
            run.SortFrom(start);
            if (refresh.IsError)
            {
                return refresh.Error;
            }

            if (run.Limited is not null)
            {
                return true;
            }
        }

        while (!run.Enough(request.Limit))
        {
            var start = run.Items.Count;
            var chunk = await ReadChunk(request, channel, run, cancellationToken).ConfigureAwait(false);
            run.SortFrom(start);
            if (chunk.IsError)
            {
                return chunk.Error;
            }

            if (chunk.Value != ChunkEnd.More)
            {
                return true;
            }
        }

        return true;
    }

    private async ValueTask<Result<ChunkEnd>> ReadChunk(PullSlackRequest request, SlackChannelRef channel, PullRun run, CancellationToken cancellationToken)
    {
        var pass = run.Next.Pass(channel.Channel);
        var read = await ReadHistory(channel, Oldest(request, channel), pass is null ? Latest(request) : pass.Below ?? SlackTs.Next(pass.Top), run, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        if (run.Limited is not null)
        {
            return ChunkEnd.Stopped;
        }

        return await Settle(new Place(request, channel, pass), read.Value, run, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<ChunkEnd>> Settle(Place place, (List<SlackMessage> Roots, bool Exhausted) read, PullRun run, CancellationToken cancellationToken)
    {
        var (request, channel, pass) = place;
        var (roots, exhausted) = read;
        if (roots.Count == 0)
        {
            run.Next = pass is null ? run.Next : run.Next.With(channel.Channel, pass.Top);
            return ChunkEnd.Done;
        }

        var top = pass?.Top ?? roots[0].Ts;
        if (pass is null && NothingNewer(request, channel, top))
        {
            return ChunkEnd.Done;
        }

        var oldestDay = SlackRecords.DayOf(roots[^1]);
        var complete = exhausted ? roots : [.. roots.Where(m => !string.Equals(SlackRecords.DayOf(m), oldestDay, StringComparison.Ordinal))];
        var chunk = new Chunk(request, channel, top, pass?.Below, complete, roots[0].Ts);
        return await Emit(chunk, pass?.ThreadsBelow, exhausted, run, cancellationToken).ConfigureAwait(false);
    }

    // why: with no pass in progress, a first message that is not newer than the watermark means the channel has nothing new to read.
    private static bool NothingNewer(PullSlackRequest request, SlackChannelRef channel, string top)
        => request.After.Newest(channel.Channel) is { } mark && !SlackTs.IsBefore(mark, top);

    /// <summary>Emits the chunk's day records (unless an earlier pull did), reads its threads, and moves the cursor to the end of the chunk.</summary>
    private async ValueTask<Result<ChunkEnd>> Emit(Chunk chunk, string? threadsBelow, bool exhausted, PullRun run, CancellationToken cancellationToken)
    {
        if (threadsBelow is null)
        {
            run.Items.AddRange(Days(chunk.Channel, chunk.Request.Workspace, chunk.Complete));
            threadsBelow = SlackTs.Next(chunk.Newest);
            run.Next = run.Next.WithPass(chunk.Channel.Channel, new SlackPass(chunk.Top, chunk.Below, threadsBelow));
        }

        var threads = await ReadThreads(chunk, threadsBelow, run, cancellationToken).ConfigureAwait(false);
        if (threads.IsError || threads.Value != ChunkEnd.More)
        {
            return threads;
        }

        if (exhausted)
        {
            run.Next = run.Next.With(chunk.Channel.Channel, chunk.Top);
            return ChunkEnd.Done;
        }

        run.Next = run.Next.WithPass(chunk.Channel.Channel, new SlackPass(chunk.Top, SlackTs.DayStart(chunk.Complete[^1].Ts), ThreadsBelow: null));
        return ChunkEnd.More;
    }

    /// <summary>Reads history pages from <paramref name="latest"/> (exclusive) down until a message of an older day than the first appears or history runs out; a rate limit sets <see cref="PullRun.Limited"/>.</summary>
    private async ValueTask<Result<(List<SlackMessage> Roots, bool Exhausted)>> ReadHistory(SlackChannelRef channel, string? oldest, string? latest, PullRun run, CancellationToken cancellationToken)
    {
        List<SlackMessage> roots = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        for (var page = 0; page < MaxPagesPerChunk; page++)
        {
            var read = await reader.HistoryPage(channel.Channel, oldest, latest, PageSize, cancellationToken).ConfigureAwait(false);
            if (read.IsError)
            {
                return read.Error;
            }

            if (read.Value.RateLimitedUntil is { } until)
            {
                run.Limited = until;
                return (roots, false);
            }

            // why: Slack's inclusive flag can return the message AT latest; the bound is exclusive, so drop it, and drop a message a page boundary repeated.
            roots.AddRange(read.Value.Items.Where(m => (latest is null || SlackTs.IsBefore(m.Ts, latest)) && seen.Add(m.Ts)));
            var next = OldestTs(read.Value.Items);
            if (!read.Value.HasMore || next is null || !Descends(next, latest))
            {
                return (roots, true);
            }

            latest = next;
            if (!string.Equals(SlackRecords.DayOf(roots[0]), SlackRecords.DayOf(roots[^1]), StringComparison.Ordinal))
            {
                return (roots, false);
            }
        }

        return ConnectorsErrors.BadReply.With($"The channel has more than {MaxPagesPerChunk} pages of messages in one day; narrow context.index.windowDays.");
    }

    // why: a page that does not reach below its own bound would repeat forever, so it counts as the end of history.
    internal static bool Descends(string next, string? latest) => latest is null || SlackTs.IsBefore(next, latest);

    internal static string? OldestTs(IReadOnlyList<SlackMessage> items)
        => items.Count == 0 ? null : items.Select(static m => m.Ts).Aggregate(static (a, b) => SlackTs.IsBefore(b, a) ? b : a);

    private async ValueTask<Result<ChunkEnd>> ReadThreads(Chunk chunk, string threadsBelow, PullRun run, CancellationToken cancellationToken)
    {
        foreach (var root in chunk.Complete.Where(m => !SlackRecords.IsNoise(m) && SlackRecords.IsThreadRoot(m) && SlackTs.IsBefore(m.Ts, threadsBelow)))
        {
            if (run.Enough(chunk.Request.Limit))
            {
                return ChunkEnd.Stopped;
            }

            var thread = await reader.Replies(chunk.Channel.Channel, root.Ts, cancellationToken).ConfigureAwait(false);
            if (thread.IsError)
            {
                return thread.Error;
            }

            if (thread.Value.RateLimitedUntil is { } limited)
            {
                run.Limited = limited;
                return ChunkEnd.Stopped;
            }

            run.Replies++;
            if (thread.Value.Items.Count > 0 && SlackRecords.Thread(chunk.Channel, chunk.Request.Workspace, thread.Value.Items) is { } item)
            {
                run.Items.Add(item);
                run.Next = run.Next.WithRepliesMark(chunk.Channel.Channel, thread.Value.Items.Select(static m => m.Ts).Aggregate(static (a, b) => SlackTs.Later(a, b)));
            }

            run.Next = run.Next.WithPass(chunk.Channel.Channel, new SlackPass(chunk.Top, chunk.Below, root.Ts));
        }

        return ChunkEnd.More;
    }

    private static IEnumerable<ConnectorItem> Days(SlackChannelRef channel, string workspace, IReadOnlyList<SlackMessage> roots)
        => roots
            .Where(static m => !SlackRecords.IsNoise(m) && SlackRecords.IsUnthreaded(m) && SlackRecords.DayOf(m) is not null)
            .GroupBy(static m => SlackRecords.DayOf(m)!, StringComparer.Ordinal) // why: the Where above keeps only messages whose day is not null.
            .Select(day => SlackRecords.Day(channel, workspace, day.Key, [.. day.OrderBy(static m => m.Ts, StringComparer.Ordinal)]))
            .OfType<ConnectorItem>();

    /// <summary>One chunk of a pass: its complete days' roots (newest first), the pass's top, the chunk's exclusive history bound and its newest root.</summary>
    private sealed record Chunk(PullSlackRequest Request, SlackChannelRef Channel, string Top, string? Below, List<SlackMessage> Complete, string Newest);

    private sealed record Place(PullSlackRequest Request, SlackChannelRef Channel, SlackPass? Pass);

    private enum ChunkEnd
    {
        /// <summary>The channel is finished (or has nothing new).</summary>
        Done,

        /// <summary>The chunk is finished and the pass goes on below it.</summary>
        More,

        /// <summary>The pull stopped inside the chunk (a rate limit or the replies budget); the cursor holds the place.</summary>
        Stopped,
    }
}
