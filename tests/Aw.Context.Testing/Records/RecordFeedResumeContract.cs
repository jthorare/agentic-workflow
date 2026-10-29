using System.Globalization;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Xunit;

namespace Aw.Context.Testing.Records;

/// <summary>
/// What every <see cref="RecordFeed"/> promises the sync run (issue #1679, owner rule #1682): <b>each pull is a bounded chunk, and the cursor only moves with the records it returns</b>. The run commits each page with its cursor and reads an
/// empty page as 'this phase is finished', so a feed that reads its whole source before it returns anything loses all of it to a rate limit or the time cap, and one that answers a rate limit with an empty page ends the phase. A binding
/// gives a <see cref="ResumeUpstream"/> holding a source bigger than one pull, and says how many requests a pull may make. The facts: (1) every pull before an interruption returns records and a cursor, within the request bound;
/// (2) the interrupted pull is an error, never an empty page, and asking again from the last cursor is an error too; (3) after the interruption lifts, pulling from the last returned cursor finishes the source with every record exactly once by key
/// (a binding names the keys it may repeat) and without asking again for what the returned pages read; (4) the interruption early and late in the source both resume.
/// The same facts hold when the interruption is a <b>cancellation</b> (the feed's time cap, SND-CONTEXT-059) after N requests, early and late: the upstream cancels the pull's token and throws, a pull cancelled mid-way is an error or a throw (never an empty page; any records it
/// returns are not counted as kept, because the run commits with the same cancelled token), and a fresh token resumes from the last cursor of a pull that finished before the cancel. Progress rests on the bounded pulls, not on salvage.
/// </summary>
public abstract class RecordFeedResumeContract
{
    private const int MaxPulls = 200;

    private string Owner => GetType().Name;

    /// <summary>Gets how many records a pull asks for.</summary>
    protected abstract int Limit { get; }

    /// <summary>Gets the most upstream requests one pull may make.</summary>
    protected abstract int MaxRequestsPerPull { get; }

    /// <summary>Gets how many requests are served before the early interruption; the first pull must still make progress.</summary>
    protected abstract int EarlyInterruption { get; }

    /// <summary>Gets how many requests are served before the late interruption; it must land after more pages than the early one.</summary>
    protected abstract int LateInterruption { get; }

    /// <summary>A fresh upstream holding a source bigger than <see cref="Limit"/> records, and a feed over it.</summary>
    /// <returns>The upstream.</returns>
    protected abstract ResumeUpstream Create();

    /// <summary>Whether a record may come again after a resume (Slack rebuilds a day record when a pull resumes inside that day). Every other key comes exactly once.</summary>
    /// <param name="key">The record key.</param>
    /// <returns>True when the key may repeat.</returns>
    protected virtual bool MayRepeatKey(string key) => false;

    /// <summary>Whether an upstream request may be made again after a resume although a returned page already made it (a feed that re-reads the cheap list to find its place). Every other request is made once.</summary>
    /// <param name="request">The request as the upstream logged it.</param>
    /// <returns>True when the request may repeat.</returns>
    protected virtual bool MayRepeatRequest(string request) => false;

    /// <summary>Every pull before the interruption returns records and a cursor, and none makes more requests than the bound.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public virtual async Task Every_pull_before_an_interruption_returns_records_a_cursor_and_a_bounded_number_of_requests()
    {
        foreach (var point in new[] { EarlyInterruption, LateInterruption })
        {
            var run = await Interrupt(point);

            ContractCheck.That(Owner, run.Pulls.Count > 0, $"a pull made progress before the interruption after {N(point)} requests");
            foreach (var pull in run.Pulls)
            {
                ContractCheck.That(Owner, pull.Items.Count > 0, "a pull before the interruption returns records");
                ContractCheck.That(Owner, !string.IsNullOrEmpty(pull.Cursor.Value), "a pull before the interruption returns a cursor");
                ContractCheck.That(Owner, pull.Requests <= MaxRequestsPerPull, $"a pull makes at most {N(MaxRequestsPerPull)} upstream requests (made {N(pull.Requests)})");
            }

            ContractCheck.That(Owner, run.FailedRequests <= MaxRequestsPerPull, $"the interrupted pull makes at most {N(MaxRequestsPerPull)} upstream requests (made {N(run.FailedRequests)})");
        }
    }

    /// <summary>The interrupted pull is an error, not an empty page (the run would read that as the end of the phase), and asking again from the last cursor is an error too.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public virtual async Task An_interrupted_pull_is_an_error_and_never_an_empty_page()
    {
        foreach (var point in new[] { EarlyInterruption, LateInterruption })
        {
            var run = await Interrupt(point);

            ContractCheck.That(Owner, !string.IsNullOrEmpty(run.Failure?.Code), "the interrupted pull is an error with a code");
            var again = await run.Upstream.Feed.Pull(new RecordPull(run.LastCursor, Limit, NotBefore: null), TestContext.Current.CancellationToken);
            ContractCheck.That(Owner, again.IsError, "asking again from the last cursor while the upstream is still limited is an error, never an empty page");
        }
    }

    /// <summary>After the interruption lifts, pulling from the last returned cursor finishes the source with every record once by key.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public virtual async Task Resuming_from_the_last_returned_cursor_finishes_the_source_with_every_record_once()
    {
        foreach (var point in new[] { EarlyInterruption, LateInterruption })
        {
            var run = await Interrupt(point);
            var resumed = await Resume(run);

            var counts = run.Pulls.SelectMany(static p => p.Items).Concat(resumed.Items).GroupBy(static k => k, StringComparer.Ordinal).ToDictionary(static g => g.Key, static g => g.Count(), StringComparer.Ordinal);
            foreach (var key in run.Upstream.Keys)
            {
                ContractCheck.That(Owner, counts.ContainsKey(key), $"the source's record {key} came (interruption after {N(point)} requests)");
                ContractCheck.That(Owner, counts[key] == 1 || MayRepeatKey(key), $"the source's record {key} came {N(counts.GetValueOrDefault(key))} times, not once (interruption after {N(point)} requests)");
            }

            var stray = counts.Keys.Where(k => !run.Upstream.Keys.Contains(k, StringComparer.Ordinal)).ToList();
            ContractCheck.That(Owner, stray.Count == 0, "no record the source does not hold came: " + string.Join(", ", stray));
        }
    }

    /// <summary>The resume does not ask the upstream again for what the pages already returned read.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public virtual async Task Resuming_does_not_ask_again_for_what_the_returned_pages_read()
    {
        foreach (var point in new[] { EarlyInterruption, LateInterruption })
        {
            var run = await Interrupt(point);
            var resumed = await Resume(run);

            var returned = run.Upstream.Served.Take(run.ServedAtLastReturnedPage).ToHashSet(StringComparer.Ordinal);
            var again = resumed.Requests.Where(r => returned.Contains(r) && !MayRepeatRequest(r)).ToList();
            ContractCheck.That(Owner, again.Count == 0, $"the resume asked again for {string.Join("; ", again.Take(3))} (interruption after {N(point)} requests)");
        }
    }

    /// <summary>The early and the late interruption land after different pages, and both resume.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public virtual async Task The_early_and_the_late_interruption_both_resume()
    {
        var early = await Interrupt(EarlyInterruption);
        var late = await Interrupt(LateInterruption);

        ContractCheck.That(Owner, late.Pulls.Sum(static p => p.Items.Count) > early.Pulls.Sum(static p => p.Items.Count), "the late interruption lands after more records than the early one");
        _ = await Resume(early);
        _ = await Resume(late);
    }

    /// <summary>A pull cancelled mid-way throws or is an error, never an empty page, and every pull before the cancel returns records, a cursor and stays within the request bound. Records a cancelled pull returns are not kept.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public virtual async Task A_pull_cancelled_mid_way_keeps_the_pulls_before_it_and_is_never_an_empty_page()
    {
        foreach (var point in await CancelPoints())
        {
            var run = await Cancel(point);

            ContractCheck.That(Owner, run.Pulls.Count > 0, $"a pull made progress before the cancel after {N(point)} requests");
            foreach (var pull in run.Pulls)
            {
                ContractCheck.That(Owner, pull.Items.Count > 0, "a pull before the cancel returns records");
                ContractCheck.That(Owner, !string.IsNullOrEmpty(pull.Cursor.Value), "a pull before the cancel returns a cursor");
                ContractCheck.That(Owner, pull.Requests <= MaxRequestsPerPull, $"a pull makes at most {N(MaxRequestsPerPull)} upstream requests (made {N(pull.Requests)})");
            }

            ContractCheck.That(Owner, run.FailedRequests <= MaxRequestsPerPull, $"the cancelled pull makes at most {N(MaxRequestsPerPull)} upstream requests (made {N(run.FailedRequests)})");
        }
    }

    /// <summary>After a cancel, a fresh token resumes from the last cursor a finished pull returned: every record once by key, nothing the returned pages read asked again.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public virtual async Task Resuming_with_a_fresh_token_after_a_cancel_finishes_the_source_without_repeating_work()
    {
        foreach (var point in await CancelPoints())
        {
            var run = await Cancel(point);
            var resumed = await Resume(run);

            var counts = run.Pulls.SelectMany(static p => p.Items).Concat(resumed.Items).GroupBy(static k => k, StringComparer.Ordinal).ToDictionary(static g => g.Key, static g => g.Count(), StringComparer.Ordinal);
            foreach (var key in run.Upstream.Keys)
            {
                ContractCheck.That(Owner, counts.ContainsKey(key), $"the source's record {key} came (cancel after {N(point)} requests)");
                ContractCheck.That(Owner, counts[key] == 1 || MayRepeatKey(key), $"the source's record {key} came {N(counts.GetValueOrDefault(key))} times, not once (cancel after {N(point)} requests)");
            }

            var stray = counts.Keys.Where(k => !run.Upstream.Keys.Contains(k, StringComparer.Ordinal)).ToList();
            ContractCheck.That(Owner, stray.Count == 0, "no record the source does not hold came: " + string.Join(", ", stray));

            var returned = run.Upstream.Served.Take(run.ServedAtLastReturnedPage).ToHashSet(StringComparer.Ordinal);
            var again = resumed.Requests.Where(r => returned.Contains(r) && !MayRepeatRequest(r)).ToList();
            ContractCheck.That(Owner, again.Count == 0, $"the resume asked again for {string.Join("; ", again.Take(3))} (cancel after {N(point)} requests)");
        }
    }

    // The cancel points: the first request of the second pull and of the third, found by pulling a fresh upstream through uncancelled. The rate-limit points may sit inside the first pull (a feed keeps what it has when limited),
    // where a cancel leaves no finished pull to resume from; a cancel lands between pulls, which is what the time cap does to a run that commits page by page.
    private async Task<int[]> CancelPoints()
    {
        var upstream = Create();
        SourceCursor? cursor = null;
        List<int> boundaries = [];
        for (var i = 0; i < MaxPulls && boundaries.Count < 2; i++)
        {
            var result = await upstream.Feed.Pull(new RecordPull(cursor, Limit, NotBefore: null), TestContext.Current.CancellationToken);
            ContractCheck.That(Owner, result.IsSuccess && result.Value.Items.Count > 0, "the source holds at least three pulls' worth, so a cancel can land after the first and after the second");
            cursor = result.Value.Next;
            boundaries.Add(upstream.Attempts);
        }

        ContractCheck.That(Owner, boundaries.Count == 2, "the source holds at least three pulls' worth, so a cancel can land after the first and after the second");
        return [.. boundaries];
    }

    // One pull; null when the cancel reached it as a throw.
    private async Task<Result<RecordPage>?> PullCancellable(ResumeUpstream upstream, SourceCursor? cursor, CancellationToken token)
    {
        try
        {
            return await upstream.Feed.Pull(new RecordPull(cursor, Limit, NotBefore: null), token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return null;
        }
    }

    // Pulls from the start under a token the upstream cancels after 'point' requests, until a pull throws or fails; only the pulls that finished before the cancel count as returned to the run.
    private async Task<InterruptedRun> Cancel(int point)
    {
        var upstream = Create();
        ContractCheck.That(Owner, upstream.Keys.Count > Limit, "the source holds more records than one pull asks for");
        using var source = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        upstream.CancelAfter(point, source);
        List<Pulled> pulls = [];
        SourceCursor? cursor = null;
        var served = 0;
        for (var i = 0; i < MaxPulls; i++)
        {
            var before = upstream.Attempts;
            var result = await PullCancellable(upstream, cursor, source.Token);
            var used = upstream.Attempts - before;
            if (result is not { } pulled)
            {
                return new InterruptedRun(upstream, pulls, Failure: null, cursor, served, used);
            }

            if (pulled.IsError)
            {
                return new InterruptedRun(upstream, pulls, pulled.Error, cursor, served, used);
            }

            var page = pulled.Value;
            var empty = page.Items.Count == 0 && page.Removed.Count == 0;
            ContractCheck.That(Owner, !empty, upstream.Interrupted
                ? "a pull cancelled mid-way answered an empty page, which the run reads as 'the phase is finished'"
                : $"the source ended before the cancel after {N(point)} requests; make the source bigger or the cancel earlier");
            if (upstream.Interrupted)
            {
                // it returned records although the token was cancelled: the run commits with that token, so they are not kept
                return new InterruptedRun(upstream, pulls, Failure: null, cursor, served, used);
            }

            pulls.Add(new Pulled([.. page.Items.Select(static item => item.Key.Value)], page.Next, used));
            cursor = page.Next;
            served = upstream.Served.Count;
        }

        throw new ContractViolationException($"{Owner}: no pull was cancelled in {N(MaxPulls)} pulls; the cancel after {N(point)} requests never fired");
    }

    // Pulls from the start with the upstream interrupted after 'point' requests, until a pull fails; every success before it counts as returned to the run.
    private async Task<InterruptedRun> Interrupt(int point)
    {
        var upstream = Create();
        ContractCheck.That(Owner, upstream.Keys.Count > Limit, "the source holds more records than one pull asks for");
        upstream.InterruptAfter(point);
        List<Pulled> pulls = [];
        SourceCursor? cursor = null;
        var served = 0;
        for (var i = 0; i < MaxPulls; i++)
        {
            var before = upstream.Attempts;
            var result = await upstream.Feed.Pull(new RecordPull(cursor, Limit, NotBefore: null), TestContext.Current.CancellationToken);
            var used = upstream.Attempts - before;
            if (result.IsError)
            {
                return new InterruptedRun(upstream, pulls, result.Error, cursor, served, used);
            }

            var page = result.Value;
            ContractCheck.That(Owner, page.Items.Count > 0 || page.Removed.Count > 0, upstream.Interrupted
                ? "a pull that hit the interruption answered an empty page, which the run reads as 'the phase is finished'"
                : $"the source ended before the interruption after {N(point)} requests; make the source bigger or the interruption earlier");
            pulls.Add(new Pulled([.. page.Items.Select(static item => item.Key.Value)], page.Next, used));
            cursor = page.Next;
            served = upstream.Served.Count;
        }

        throw new ContractViolationException($"{Owner}: no pull failed in {N(MaxPulls)} pulls; the interruption after {N(point)} requests never fired");
    }

    // Lifts the interruption and pulls from the last returned cursor until the first empty page.
    private async Task<Resumed> Resume(InterruptedRun run)
    {
        var upstream = run.Upstream;
        upstream.Lift();
        var servedAtLift = upstream.Served.Count;
        List<string> items = [];
        var cursor = run.LastCursor;
        for (var i = 0; i < MaxPulls; i++)
        {
            var before = upstream.Attempts;
            var result = await upstream.Feed.Pull(new RecordPull(cursor, Limit, NotBefore: null), TestContext.Current.CancellationToken);
            ContractCheck.That(Owner, result.IsSuccess, "a pull after the interruption lifted succeeds (" + (result.IsError ? result.Error.Code + " " + result.Error.Message : string.Empty) + ")");
            ContractCheck.That(Owner, upstream.Attempts - before <= MaxRequestsPerPull, $"a resumed pull makes at most {N(MaxRequestsPerPull)} upstream requests (made {N(upstream.Attempts - before)})");
            var page = result.Value;
            if (page.Items.Count == 0 && page.Removed.Count == 0)
            {
                return new Resumed(items, [.. upstream.Served.Skip(servedAtLift)]);
            }

            items.AddRange(page.Items.Select(static item => item.Key.Value));
            cursor = page.Next;
        }

        throw new ContractViolationException($"{Owner}: the resumed source did not end in {N(MaxPulls)} pulls");
    }

    private static string N(int n) => n.ToString(CultureInfo.InvariantCulture);

    private sealed record Pulled(IReadOnlyList<string> Items, SourceCursor Cursor, int Requests);

    private sealed record InterruptedRun(ResumeUpstream Upstream, List<Pulled> Pulls, Error? Failure, SourceCursor? LastCursor, int ServedAtLastReturnedPage, int FailedRequests);

    private sealed record Resumed(List<string> Items, List<string> Requests);
}
