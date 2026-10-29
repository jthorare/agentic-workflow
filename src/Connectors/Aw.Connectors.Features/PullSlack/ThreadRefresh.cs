using Aw.Connectors.Core.Slack;
using Aw.Kernel;

namespace Aw.Connectors.Features.PullSlack;

/// <summary>
/// The thread refresh a pull makes before it looks for newer roots (issue #1689): history from <see cref="RefreshDays"/> days before the watermark's day up to the watermark is read, and every thread root whose
/// latest reply is newer than the channel's replies mark is read again, oldest latest reply first. Each emitted thread moves the mark, so a rate limit or the replies budget resumes at the next candidate; nothing
/// changed means nothing emitted and nothing moved. A thread whose root is older than the window is not re-read. When a pull read every candidate and left no pass in progress, the mark also moves to the pull's cutoff
/// (see <see cref="PullSlackService"/>); a pass that spans several pulls never moves it, so its threads are read once more by the next refresh.
/// </summary>
internal sealed class ThreadRefresh(ISlackReader reader)
{
    /// <summary>How many days before the watermark's day a pull looks for thread roots with new replies.</summary>
    internal const int RefreshDays = 7;

    /// <summary>How far before the pull's start the replies mark may be moved: the margin covers clock skew between this box and Slack.</summary>
    internal static readonly TimeSpan ClockMargin = TimeSpan.FromMinutes(5);

    /// <summary>Reads the candidates; true when it read all of them, false when the replies budget or a rate limit stopped it.</summary>
    public async ValueTask<Result<bool>> Run(PullSlackRequest request, SlackChannelRef channel, string watermark, PullRun run, CancellationToken cancellationToken)
    {
        var mark = run.Next.RepliesMark(channel.Channel) ?? watermark;
        var roots = await ReadRoots(channel, watermark, run, cancellationToken).ConfigureAwait(false);
        if (roots.IsError)
        {
            return roots.Error;
        }

        run.ScanNewest = roots.Value.SelectMany(static m => new[] { m.Ts, m.LatestReply }).OfType<string>().Aggregate((string?)null, static (best, ts) => best is null ? ts : SlackTs.Later(best, ts));
        foreach (var root in Candidates(roots.Value, mark))
        {
            if (run.Enough(request.Limit))
            {
                return false;
            }

            var read = await ReadOne(request, channel, root, run, cancellationToken).ConfigureAwait(false);
            if (read.IsError || run.Limited is not null)
            {
                return read.IsError ? read : false;
            }
        }

        return run.Limited is null;
    }

    private static IOrderedEnumerable<SlackMessage> Candidates(List<SlackMessage> roots, string mark)
        => roots
            .Where(m => !SlackRecords.IsNoise(m) && SlackRecords.IsThreadRoot(m) && m.LatestReply is { } latest && SlackTs.IsBefore(mark, latest))
            .OrderBy(static m => m.LatestReply, SlackTsComparer.Instance);

    private async ValueTask<Result<bool>> ReadOne(PullSlackRequest request, SlackChannelRef channel, SlackMessage root, PullRun run, CancellationToken cancellationToken)
    {
        var thread = await reader.Replies(channel.Channel, root.Ts, cancellationToken).ConfigureAwait(false);
        if (thread.IsError)
        {
            return thread.Error;
        }

        if (thread.Value.RateLimitedUntil is { } limited)
        {
            run.Limited = limited;
            return true;
        }

        run.Replies++;
        if (thread.Value.Items.Count > 0 && SlackRecords.Thread(channel, request.Workspace, thread.Value.Items) is { } item)
        {
            run.Items.Add(item);
            // why: the mark moves only to what the history scan proved (never to the live thread's newest reply, which may be past another candidate); only roots with a latest reply are candidates.
            run.Next = run.Next.WithRepliesMark(channel.Channel, root.LatestReply!);
        }

        return true;
    }

    /// <summary>The top-level messages of the refresh window, read in bounded pages; a rate limit sets <see cref="PullRun.Limited"/>.</summary>
    private async ValueTask<Result<List<SlackMessage>>> ReadRoots(SlackChannelRef channel, string watermark, PullRun run, CancellationToken cancellationToken)
    {
        var oldest = SlackTs.TryParse(SlackTs.DayStart(watermark), out var day) ? SlackTs.Format(day.AddDays(-RefreshDays)) : null;
        var latest = SlackTs.Next(watermark);
        List<SlackMessage> roots = [];
        for (var page = 0; page < PullSlackService.MaxPagesPerChunk; page++)
        {
            var read = await reader.HistoryPage(channel.Channel, oldest, latest, PullSlackService.PageSize, cancellationToken).ConfigureAwait(false);
            if (read.IsError)
            {
                return read.Error;
            }

            if (read.Value.RateLimitedUntil is { } until)
            {
                run.Limited = until;
                return roots;
            }

            var bound = latest;
            roots.AddRange(read.Value.Items.Where(m => SlackTs.IsBefore(m.Ts, bound)));
            var next = PullSlackService.OldestTs(read.Value.Items);
            if (!read.Value.HasMore || next is null || !PullSlackService.Descends(next, latest))
            {
                break;
            }

            latest = next;
        }

        return roots;
    }

    private sealed class SlackTsComparer : IComparer<string?>
    {
        public static SlackTsComparer Instance { get; } = new();

        public int Compare(string? x, string? y) => x is null || y is null ? 0 : SlackTs.IsBefore(x, y) ? -1 : SlackTs.IsBefore(y, x) ? 1 : 0;
    }
}
