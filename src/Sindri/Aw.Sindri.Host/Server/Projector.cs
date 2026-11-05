using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Host.Server;

/// <summary>
/// Folds the ledger into views.db (API spec §2.3), one pass at a time under one gate, as the caller 'service:projector'.
/// A pass opens the store (rebuilding when views.db was just created, its schema version is old, or a rebuild was
/// requested), reads the head, publishes it, then applies pages of 100 until the cursor reaches that head. A failing
/// event stalls it (SND-VIEW-001, logged); a stalled projector retries 30 s later, or at once on a rebuild. A fold that
/// read back a stored row it could not deserialize (an older build's shape, #959) saw it as absent; the projector then
/// rebuilds the views from the ledger on its own, once, right after the pass.
/// </summary>
internal sealed partial class Projector(
    ILedgerReader ledger, IViewStore store, IEnumerable<IProjection> projections, IProjectorFeed feed, ICallerContextAccessor callers,
    TimeProvider clock, ILogger<Projector> log) : IDisposable
{
    internal const int PageSize = 100;
    internal static readonly TimeSpan StallRetry = TimeSpan.FromSeconds(30);

    private readonly IReadOnlyList<IProjection> _projections = [.. projections];
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset? _stalledSince;
    private string? _problem;
    private long _seenHead;
    private bool _healing;
    private ViewsStatus _last = new(0, 0, ProjectedAt: null, Rebuilding: false, Stalled: false, StalledAt: null, StalledType: null, Starting: true);

    /// <summary>The ledger head the last pass saw; the host waits for an append past it.</summary>
    internal long SeenHead => Volatile.Read(ref _seenHead);

    /// <inheritdoc/>
    public void Dispose() => _gate.Dispose();

    /// <summary>One pass: rebuild if needed, then fold up to the ledger head.</summary>
    /// <param name="cancellationToken">Cancels the pass.</param>
    /// <returns>How it ended.</returns>
    public async ValueTask<ProjectorPass> CatchUp(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var caller = callers.EnterService("projector");
            var pass = await Pass(cancellationToken).ConfigureAwait(false);
            return _healing && pass != ProjectorPass.Failed ? await Pass(cancellationToken).ConfigureAwait(false) : pass;
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    private async ValueTask<ProjectorPass> Pass(CancellationToken ct)
    {
        var rebuild = feed.RebuildRequested;   // taken only once the reset worked: a failed attempt keeps the request
        if (!rebuild && !_healing && _stalledSince is { } since && clock.GetUtcNow() - since < StallRetry)
        {
            return ProjectorPass.Stalled;
        }

        var head = await ledger.Head(ct).ConfigureAwait(false);
        var opened = head.IsError ? head.Error : await Prepare(rebuild, head.Value, ct).ConfigureAwait(false);
        if (opened.IsError)
        {
            return Failed(opened.Error.Code, opened.Error.Message, head.IsSuccess ? head.Value : null);
        }

        return await Fold(opened.Value, head.Value, ct).ConfigureAwait(false);
    }

    private async ValueTask<Kernel.Result<ProjectorState>> Prepare(bool requested, long head, CancellationToken ct)
    {
        var opened = await store.Open(ViewSchema.Version, ct).ConfigureAwait(false);
        if (opened.IsError)
        {
            return opened;
        }

        var state = opened.Value;
        var reason = requested ? "requested" : _healing ? "unreadable rows" : state.Fresh ? "views.db created" : state.SchemaVersion != ViewSchema.Version ? "schema version changed" : null;
        if (reason is null)
        {
            return state;
        }

        LogRebuilding(log, reason, state.SchemaVersion, ViewSchema.Version, head);
        IReadOnlyList<string> views = [.. ViewNames.All.Concat(_projections.SelectMany(p => p.Views)).Distinct(StringComparer.Ordinal)];
        var reset = await store.Reset(views, ViewSchema.Version, clock.GetUtcNow(), ct).ConfigureAwait(false);
        if (reset.IsSuccess)
        {
            _stalledSince = null;
            _healing = false;
            if (requested)
            {
                _ = feed.TakeRebuildRequest();
            }
        }

        return reset;
    }

    private async ValueTask<ProjectorPass> Fold(ProjectorState state, long head, CancellationToken ct)
    {
        Publish(state, head);
        while (state.Cursor < head)
        {
            var page = await ledger.ReadAfter(state.Cursor, PageSize, ct).ConfigureAwait(false);
            var applied = page.IsError ? page.Error
                : page.Value.Count == 0 ? new PageApplied(state, Stall: null)
                : await store.Apply(page.Value, _projections, clock.GetUtcNow(), ct).ConfigureAwait(false);
            if (applied.IsError)
            {
                return Failed(applied.Error.Code, applied.Error.Message, head);
            }

            state = applied.Value.State;
            NoteUnreadable(applied.Value);
            if (applied.Value.Stall is { } stall)
            {
                return Stalled(state, head, stall);
            }

            Publish(state, head);
            if (page.Value.Count == 0)
            {
                break;
            }
        }

        return await Finish(state, head, ct).ConfigureAwait(false);
    }

    private async ValueTask<ProjectorPass> Finish(ProjectorState state, long head, CancellationToken ct)
    {
        if (state.Rebuilding)
        {
            var finished = await store.FinishRebuild(ct).ConfigureAwait(false);
            if (finished.IsError)
            {
                return Failed(finished.Error.Code, finished.Error.Message, head);
            }

            state = state with { Rebuilding = false };
        }

        _stalledSince = null;
        _problem = null;
        Publish(state, head);
        return ProjectorPass.CaughtUp;
    }

    private void NoteUnreadable(PageApplied page)
    {
        // a replay writes every row in the current shape, so unreadable rows during one are not an older build's: no second rebuild
        if (page.Unreadable == 0 || page.State.Rebuilding || _healing)
        {
            return;
        }

        _healing = true;
        LogUnreadable(log, page.Unreadable);
    }

    private ProjectorPass Stalled(ProjectorState state, long head, ViewStall stall)
    {
        if (_stalledSince is null)
        {
            LogStalled(log, stall.Sequence, stall.Type, stall.Code, stall.Detail);
        }

        _stalledSince = clock.GetUtcNow();
        Publish(state, head);
        return ProjectorPass.Stalled;
    }

    /// <summary>Publishes the stalled shape for a pass that threw: no event is to blame, so StalledAt stays null (#2053).</summary>
    /// <param name="error">What the pass threw.</param>
    internal void Threw(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        Publish(_last with { Starting = false, Stalled = true, StalledAt = null, StalledType = $"{error.GetType().Name}: {error.Message}" });
    }

    private ProjectorPass Failed(string code, string detail, long? head)
    {
        // no event is to blame, so StalledAt stays null: the status says the projector can't run, until a pass works
        Publish(_last with { LedgerHead = head ?? _last.LedgerHead, Starting = false, Stalled = true, StalledAt = null, StalledType = $"{code}: {detail}" });
        if (!string.Equals(_problem, detail, StringComparison.Ordinal))
        {
            _problem = detail;
            LogFailed(log, code, detail);
        }

        return ProjectorPass.Failed;
    }

    private void Publish(ProjectorState state, long head)
    {
        Publish(new ViewsStatus(state.Cursor, head, state.ProjectedAt, state.Rebuilding, state.StalledAt is not null, state.StalledAt, state.StalledType));
        ProjectorTelemetry.Lag.Record(Math.Max(head - state.Cursor, 0));
    }

    private void Publish(ViewsStatus status)
    {
        _last = status;
        Volatile.Write(ref _seenHead, status.LedgerHead);
        feed.Publish(status);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Views rebuilding ({Reason}; schema {From} -> {To}) up to ledger head {Head}")]
    private static partial void LogRebuilding(ILogger logger, string reason, int from, int to, long head);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SND-VIEW-001 {Sequence} {Type}: the projector stalled: {Code} {Detail}; it retries every 30 s, or run sindri views rebuild")]
    private static partial void LogStalled(ILogger logger, long sequence, string type, string code, string detail);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Count} stored view row(s) are unreadable under the current shape (an older build wrote them); the fold saw them as absent, and the views rebuild from the ledger")]
    private static partial void LogUnreadable(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The projector couldn't run: {Code} {Detail}")]
    private static partial void LogFailed(ILogger logger, string code, string detail);
}
