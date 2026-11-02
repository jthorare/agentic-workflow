using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Trust;
using Aw.Kernel;

namespace Aw.Context.Features.SyncSources;

/// <summary>
/// One sync tick of one <see cref="RecordFeed"/> (owned-index spec §5.5): read the stored position, pick the phase it names, pull a page, build the batch with
/// <see cref="RecordBatches"/>, commit it (one transaction, the store saves the position with it) and go on until the feed has nothing more, the page cap or the time
/// cap. Recent reads the last 30 days newest first, Backfill the rest back to the window's start ('windowDays') from its own cursor, Incremental the changes since
/// the cursor; a phase ends on an empty page, which is committed as a move to the next phase, so a stopped tick resumes in the same phase. Nothing older than the
/// window is ever pulled. A feed that is down is the row's code, never a thrown call; a replay of a page commits unchanged records. Reconcile (the daily full-window
/// diff for deletes) is not driven yet: a stored Reconcile position is read as Incremental.
/// </summary>
internal sealed class RecordSyncRun(RecordFeed feed, IRecordStore store, (Scrubbers Scrubbers, ContextIndexSettings Index) context, TimeProvider clock, int pageSize = RecordSyncRun.PageSize)
{
    /// <summary>The most pages one tick commits.</summary>
    internal const int PagesPerTick = 20;

    /// <summary>Records per page.</summary>
    internal const int PageSize = 100;

    /// <summary>How far back Recent reads.</summary>
    internal static readonly TimeSpan RecentWindow = TimeSpan.FromDays(30);

    private readonly DateTimeOffset _started = clock.GetUtcNow();
    private int _fetched;
    private int _written;
    private int _unchanged;

    public async ValueTask<SourceSync> Execute(CancellationToken cancellationToken)
    {
        var stored = await store.Position(feed.Source, cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return Failed(stored.Error);
        }

        var position = stored.Value;
        for (var pages = 0; pages < PagesPerTick; pages++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (clock.GetUtcNow() - _started >= SyncSourcesService.RunCap)
            {
                return Row(ContextErrors.SyncCapped.Code);
            }

            var step = await Step(position, cancellationToken).ConfigureAwait(false);
            if (step.IsError)
            {
                return Failed(step.Error);
            }

            if (step.Value is not { } after)
            {
                break;   // caught up: nothing to commit, the position already says so
            }

            position = after;
        }

        return Row(code: null);
    }

    /// <summary>Pulls and commits one page; the position after it, or null when an Incremental pull found nothing.</summary>
    private async ValueTask<Result<SyncPosition?>> Step(SyncPosition position, CancellationToken cancellationToken)
    {
        var phase = PhaseOf(position);
        if (phase == SyncPhase.Backfill && position.BackfillBefore is null)
        {
            position = position with { BackfillBefore = RecentStart() };   // a row stored before the bound existed: Recent's start now, kept from here on
        }

        var pulled = await feed.Pull(PullFor(phase, position), cancellationToken).ConfigureAwait(false);
        if (pulled.IsError)
        {
            return pulled.Error;
        }

        var page = pulled.Value;
        var ended = page.Items.Count == 0 && page.Removed.Count == 0;
        if (ended && phase == SyncPhase.Incremental)
        {
            return Results.Success<SyncPosition?>(value: null);
        }

        var after = ended ? Following(phase, position, RecentStart()) : Advance(phase, position, page);
        var batch = RecordBatches.From(feed.Source, page, phase, context.Scrubbers, clock.GetUtcNow()) with { Position = after };
        var committed = await store.Commit(batch, cancellationToken).ConfigureAwait(false);
        if (committed.IsError)
        {
            return committed.Error;
        }

        _fetched += page.Items.Count;
        _written += committed.Value.Written + committed.Value.Tombstoned;
        _unchanged += committed.Value.Unchanged;
        return Results.Success<SyncPosition?>(after);
    }

    /// <summary>The phase the next pull belongs to: Recent for a source that never committed, Incremental for a stored Reconcile (not driven yet).</summary>
    private static SyncPhase PhaseOf(SyncPosition position) =>
        position.Phase is null ? SyncPhase.Recent : position.Phase == SyncPhase.Reconcile ? SyncPhase.Incremental : position.Phase.Value;

    private RecordPull PullFor(SyncPhase phase, SyncPosition position)
    {
        var windowStart = clock.GetUtcNow() - TimeSpan.FromDays(context.Index.WindowDays);
        if (phase == SyncPhase.Recent)
        {
            return new RecordPull(position.Cursor, pageSize, RecentStart());
        }

        // Backfill stops where Recent started
        return phase == SyncPhase.Backfill ? new RecordPull(position.BackfillCursor, pageSize, windowStart, position.BackfillBefore) : new RecordPull(position.Cursor, pageSize, windowStart);
    }

    /// <summary>Where Recent starts now: 30 days back, but never before the window's start.</summary>
    private DateTimeOffset RecentStart()
    {
        var now = clock.GetUtcNow();
        return Max(now - RecentWindow, now - TimeSpan.FromDays(context.Index.WindowDays));
    }

    /// <summary>The position after a page with records: the cursor of the phase moves, the others stay.</summary>
    private static SyncPosition Advance(SyncPhase phase, SyncPosition position, RecordPage page) =>
        phase == SyncPhase.Backfill ? position with { Phase = phase, BackfillCursor = page.Next } : position with { Phase = phase, Cursor = page.Next };

    /// <summary>The position after an empty page: the phase is through, so the next pull belongs to the following one (the cursors stay).</summary>
    private static SyncPosition Following(SyncPhase phase, SyncPosition position, DateTimeOffset recentStart) =>
        phase == SyncPhase.Recent ? position with { Phase = SyncPhase.Backfill, BackfillBefore = recentStart } : position with { Phase = SyncPhase.Incremental };

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;

    /// <summary>The message of the error the last <see cref="Execute"/> ended with, unscrubbed; null when it did not end with one. The caller scrubs it before it is stored.</summary>
    public string? ErrorMessage { get; private set; }

    private SourceSync Failed(Error error)
    {
        ErrorMessage = error.Message;
        return Row(error.Code);
    }

    private SourceSync Row(string? code) => new(feed.Source, _written, _fetched, _unchanged, Linked: 0, Unlinked: 0, Orphaned: 0, Decisions: 0, Queued: 0, Refused: 0, Tokens: 0, code);
}
