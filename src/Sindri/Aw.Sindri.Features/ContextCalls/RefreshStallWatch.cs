namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// Ends a refresh job that has gone 30 min without a progress line (owner, 2026-11-05, #2106): every line written through it is forwarded to the job's progress and stamps the clock; when 30 min pass with no
/// stamp it cancels <see cref="Token"/> and sets <see cref="Stalled"/>. The count starts when the watch is made, so the job's start counts as a line. There is no wall-clock cap: a job that keeps writing lines runs as long as it needs.
/// </summary>
internal sealed class RefreshStallWatch : IProgress<string>, IDisposable
{
    /// <summary>How long the job may go without a progress line.</summary>
    internal static readonly TimeSpan Limit = TimeSpan.FromMinutes(30);

    private readonly IProgress<string>? _inner;
    private readonly TimeProvider _clock;
    private readonly CancellationTokenSource _cts;
    private long _lastLineTicks;
    private volatile bool _stalled;

    /// <param name="inner">The job's progress, or null when it has none.</param>
    /// <param name="clock">Stamps the lines and times the limit.</param>
    /// <param name="jobToken">The job's own cancellation; cancelling it cancels <see cref="Token"/> without setting <see cref="Stalled"/>.</param>
    internal RefreshStallWatch(IProgress<string>? inner, TimeProvider clock, CancellationToken jobToken)
    {
        _inner = inner;
        _clock = clock;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(jobToken);
        _lastLineTicks = clock.GetUtcNow().UtcTicks;
        _ = Watch(_cts.Token);
    }

    /// <summary>Cancelled when the job is cancelled or the limit passes.</summary>
    internal CancellationToken Token => _cts.Token;

    /// <summary>Whether the limit, not the job's own token, cancelled <see cref="Token"/>.</summary>
    internal bool Stalled => _stalled;

    /// <summary>When the last line was written (or the job started).</summary>
    internal DateTimeOffset LastLine => new(Interlocked.Read(ref _lastLineTicks), TimeSpan.Zero);

    public void Report(string value)
    {
        _ = Interlocked.Exchange(ref _lastLineTicks, _clock.GetUtcNow().UtcTicks);
        _inner?.Report(value);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    private async Task Watch(CancellationToken token)
    {
        try
        {
            while (true)
            {
                var due = LastLine + Limit - _clock.GetUtcNow();
                if (due <= TimeSpan.Zero)
                {
                    _stalled = true;
                    await _cts.CancelAsync().ConfigureAwait(false);
                    return;
                }

                await Task.Delay(due, _clock, token).ConfigureAwait(false);
            }
        }
        catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException)
        {
            // The job ended or was cancelled (the source may be gone by now): nothing left to watch.
        }
    }
}
