using System.Threading.Channels;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// S1b's loop behind <see cref="HealthViewModel"/>: it finds the control plane through its endpoint record, checks the
/// record against /sindri/ping (a stale record reads as not running), reads the catalog (/sindri/commands) and
/// /sindri/status, and after each event on /sindri/events re-reads every view it shows (the status and, for the
/// stalled and rebuilding banners, /sindri/services), at most once per view per 250 ms: it never applies an event's payload (UI spec §9.2). While it follows, it pings every 10 s: a quiet control plane (nothing scheduled) sends
/// no events, and an answer from the process it connected to keeps the views fresh (UI spec §4.1: stale is "no event or
/// ping for 30 s"; the stream sends no keep-alive). When the stream ends, a refresh fails or the record changes, it looks
/// again every 2 s.
/// Every await resumes on the caller's context (the UI thread in the app), so what it tells the view model reaches the
/// bindings there.
/// </summary>
/// <param name="records">Where the control plane recorded its endpoint.</param>
/// <param name="api">Its API.</param>
/// <param name="clock">The clock for retry waits and pings.</param>
/// <param name="health">The view model it tells.</param>
internal sealed partial class ControlPlaneFollower(IEndpointRecordSource records, ISindriApi api, TimeProvider clock, HealthViewModel health)
{
    private long _cursor;
    private byte[] _record = [];
    private long _epoch;
    private int _pid;
    private bool _again;   // the last refresh found the views behind: read again on the next throttle beat

    /// <summary>Follows the control plane until cancelled.</summary>
    /// <param name="cancellationToken">Stops following.</param>
    /// <returns>A task that ends when cancelled.</returns>
    internal async Task Run(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var wait = await Cycle(cancellationToken).ConfigureAwait(true);

            if (wait)
            {
                try
                {
                    await Task.Delay(HealthViewModel.RetryEvery, clock, cancellationToken).ConfigureAwait(true);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    // The platform couldn't read the record (no permission, a directory in its place): say why in the app's words, from
    // the port's reason (review run 4, architecture-strategist-0), and keep trying. The platform's message names the
    // record's full path, so it stays out of the window; the path shows shortened on its own line, as everywhere else
    // (review run 3, security-sentinel-0).
    private Uri? RecordUnreadable(EndpointRecordFailure reason)
    {
        var why = reason switch
        {
            EndpointRecordFailure.NoPermission => Labels.RecordNoPermission,
            EndpointRecordFailure.NotAFile => Labels.RecordNotAFile,
            EndpointRecordFailure.Unreadable => Labels.RecordIoError,
            _ => Labels.RecordIoError,
        };
        return health.NotRunning(Labels.RecordUnreadableBecause(why), StatusText.ShortPath(records.Location));
    }

    // One connect-and-follow; true when Run should wait before the next one: nothing answers, or the event stream
    // ended at once without an event (an events route that closes), which would otherwise reconnect in a tight loop.
    private async Task<bool> Cycle(CancellationToken cancellationToken)
    {
        var url = await Connect(cancellationToken).ConfigureAwait(true);
        if (url is null)
        {
            return true;
        }

        var started = clock.GetUtcNow();
        var events = await Follow(url, cancellationToken).ConfigureAwait(true);
        return events == 0 && clock.GetUtcNow() - started < HealthViewModel.RetryEvery;
    }

    private async Task<Uri?> Connect(CancellationToken cancellationToken)
    {
        var read = await records.Read(cancellationToken).ConfigureAwait(true);
        if (read.Failure is { } unreadable)
        {
            return RecordUnreadable(unreadable);
        }

        if (read.Bytes is not { } bytes)
        {
            return health.NotRunning(Labels.NoRecord, StatusText.ShortPath(records.Location));
        }

        var record = ControlRecord.Usable(bytes, SindriApiClient.Json);
        if (record is null)
        {
            return health.NotRunning(Labels.RecordUnreadable, StatusText.ShortPath(records.Location));
        }

        if (!record.IsOnThisMachine)
        {
            return health.NotRunning(Labels.RecordUnreadableBecause(Labels.NotOnThisMachine(record.Url.Authority)), StatusText.ShortPath(records.Location));
        }

        return await Handshake(record, bytes, cancellationToken).ConfigureAwait(true);
    }

    // Pings the record's endpoint, rejects a stale record, then loads the catalog and the first status; the url to follow, or null.
    private async Task<Uri?> Handshake(ControlRecord record, byte[] bytes, CancellationToken cancellationToken)
    {
        var ping = await api.Ping(record.Url, cancellationToken).ConfigureAwait(true);
        if (ping.IsError)
        {
            return health.Failed(ping.Error, record.Url);
        }

        if (ping.Value.Pid != record.Pid || ping.Value.Epoch != record.Epoch)
        {
            return health.NotRunning(Labels.StaleRecord, Labels.StaleRecordDetail(record.Pid, record.Epoch, ping.Value.Pid, ping.Value.Epoch));
        }

        _record = bytes;
        Track(ping.Value);
        if (await api.Commands(record.Url, cancellationToken).ConfigureAwait(true) is { IsSuccess: true } catalog)
        {
            health.ShowCatalog(record.Url, catalog.Value);   // §9.1: on connect and after every reconnect
        }

        // The url is followed once a status was shown: should the views still be behind, connect again (best effort).
        return await Refresh(record.Url, cancellationToken).ConfigureAwait(true) == Refreshed.Shown ? record.Url : null;
    }

    // Follows the stream until it ends, a status refresh fails, or the endpoint record changes. A stopping control plane
    // can hold its stream open while it shuts down, so the stream ending is not the only sign that it's gone.
    // A lower epoch, or the same epoch held by another process, is a new ledger (the lease lives in it): its sequence
    // starts over. The same process at the same epoch is a dropped stream, which resumes from the cursor.
    private void Track(PingView ping)
    {
        if (ping.Epoch < _epoch || (ping.Epoch == _epoch && ping.Pid != _pid))
        {
            _cursor = 0;
        }

        (_epoch, _pid) = (ping.Epoch, ping.Pid);
        health.Follows(ping.Pid, ping.Epoch);
    }

    private async Task<int> Follow(Uri url, CancellationToken cancellationToken)
    {
        var events = 0;
        var dirty = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (_again)
        {
            _ = dirty.Writer.TryWrite(item: true);
        }

#pragma warning disable CA2025 // all three tasks are awaited together in the finally below, before 'using' disposes stop
        var refresher = RefreshOnSignal(url, dirty, stop);
        var watcher = WatchRecord(stop);
        var pinger = PingWhileFollowing(url, stop);
#pragma warning restore CA2025
        try
        {
            await foreach (var e in api.Events(url, _cursor, stop.Token).ConfigureAwait(true))
            {
                events++;
                _cursor = e.Sequence;
                health.Event(e);
                _ = dirty.Writer.TryWrite(item: true);
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            // A failed refresh or a changed record ended the stream; Run looks for the control plane again.
        }
        finally
        {
            await stop.CancelAsync().ConfigureAwait(true);
            await Task.WhenAll(refresher, watcher, pinger).ConfigureAwait(true);
        }

        return events;
    }

    // Each view re-read at most once per RefreshEvery (§9.2): the first event of a burst re-reads at once, and the
    // events that arrive in the next RefreshEvery collapse into the signal's one pending refresh, run when it ends. A
    // failed refresh ends the follow.
    private async Task RefreshOnSignal(Uri url, Channel<bool> channel, CancellationTokenSource stop)
    {
        var dirty = channel.Reader;
        try
        {
            while (await dirty.WaitToReadAsync(stop.Token).ConfigureAwait(true))
            {
                _ = dirty.TryRead(item: out _);
                if (await Refresh(url, stop.Token).ConfigureAwait(true) == Refreshed.Failed)
                {
                    await stop.CancelAsync().ConfigureAwait(true);
                    return;
                }

                await Task.Delay(HealthViewModel.RefreshEvery, clock, stop.Token).ConfigureAwait(true);
                if (_again)
                {
                    _ = channel.Writer.TryWrite(item: true);   // the projection is still behind: no event may come to say it caught up
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The follow ended.
        }
    }

    // Ends the follow when the endpoint record no longer is the one this follow connected with (stopped, or restarted).
    private async Task WatchRecord(CancellationTokenSource stop)
    {
        try
        {
            while (true)
            {
                await Task.Delay(HealthViewModel.RetryEvery, clock, stop.Token).ConfigureAwait(true);
                var now = (await records.Read(stop.Token).ConfigureAwait(true)).Bytes;   // unreadable reads as none: the next Connect reads it again and says why
                if (now is null || !now.AsSpan().SequenceEqual(_record))
                {
                    await stop.CancelAsync().ConfigureAwait(true);
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The follow ended.
        }
    }

    // Pings on its own beat while the follow lasts. An answer from the process and epoch this follow connected to is
    // heard; a failed ping or another process's answer isn't, so 30 s of them reads as stale. Ending the follow stays
    // with the stream, the refresh and the record watch, except for a refusal (Sindri's 403, e.g. SND-AUTH-001 once the
    // token is removed or replaced): a definite answer, so it ends the follow and the next connect says why.
    private async Task PingWhileFollowing(Uri url, CancellationTokenSource stop)
    {
        using var beat = new PeriodicTimer(HealthViewModel.PingEvery, clock);
        try
        {
            while (await beat.WaitForNextTickAsync(stop.Token).ConfigureAwait(true))
            {
                var ping = await api.Ping(url, stop.Token).ConfigureAwait(true);
                if (ping.IsSuccess && ping.Value.Pid == _pid && ping.Value.Epoch == _epoch)
                {
                    health.Answered();
                }
                else if (ping.IsError && ping.Error.Kind == ErrorKind.Forbidden)
                {
                    await stop.CancelAsync().ConfigureAwait(true);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The follow ended.
        }
    }
}
