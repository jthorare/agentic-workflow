using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// The in-memory Sindri API, and the model the canned HTTP handler serves: a ping, a status, the ledger's events, and
/// open event streams that get each pushed event until they are dropped (the connection ends) or the server goes down.
/// The catalog, the services view and the rebuild command answer with golden bodies (<see cref="RouteAnswer"/>), the
/// same text the handler serves.
/// </summary>
internal sealed partial class FakeSindriApi : ISindriApi
{
    private readonly Lock _gate = new();
    private readonly List<EventView> _events = [];
    private readonly List<Channel<EventView>> _streams = [];

    /// <summary>The ping answer.</summary>
    public PingView Pong { get; set; } = new(4242, 7);

    /// <summary>The status answer.</summary>
    public StatusView Current { get; set; } = A.Status();

    /// <summary>True when nothing answers.</summary>
    public bool Down { get; private set; }

    /// <summary>When true, every event stream ends as soon as it opens (an events route that answers and closes).</summary>
    public bool StreamsEndAtOnce { get; set; }

    /// <summary>When true, status calls fail as unreachable while pings and open streams carry on (a stopping process).</summary>
    public bool FailStatus { get; set; }

    /// <summary>When true, status calls answer SND-UI-003 (an answer this build can't read) while pings carry on.</summary>
    public bool UnreadableStatus { get; set; }

    /// <summary>When set, status calls answer this error (the handler serves it as a 503 problem): SND-API-011 is "views behind".</summary>
    public Error? StatusError { get; set; }

    /// <summary>
    /// How the views answer a read that carries X-Sindri-After (API §2.4, ReadYourWrites.cs): <see cref="ViewsLag.Stalled"/>
    /// and <see cref="ViewsLag.Behind"/> are both SND-API-011 (the server answers a stalled projection at once and one
    /// that is behind after its 2 s wait; the fake has no clock to wait on). A read without the sequence is never held.
    /// </summary>
    public ViewsLag Lag { get; set; }

    /// <summary>The sequence each status call carried as X-Sindri-After (0 when it carried none), in order.</summary>
    public IReadOnlyList<long> StatusAfters => [.. _statusAfters];

    private readonly List<long> _statusAfters = [];
    private long _pending;
    private (int Pid, long Epoch)? _pinged;

    /// <summary>What an unreadable status says (SND-UI-003's message), for a layout test that needs a long one.</summary>
    public string UnreadableReason { get; set; } = "/sindri/status answered 500";

    /// <summary>When set, each status call waits for it before it answers (a slow control plane); null answers at once.</summary>
    public TaskCompletionSource? StatusGate { get; set; }

    /// <summary>
    /// When set, the server refuses every call with 403 and this ProblemDetails title and detail (API §6: a human
    /// without this start's token is SND-AUTH-001): pings and status calls fail with it, and streams end at once.
    /// </summary>
    public (string Code, string Detail)? Refusal { get; set; }

    /// <summary>When true, pings fail as unreachable while status calls and open streams carry on.</summary>
    public bool FailPing { get; set; }

    /// <summary>How many status calls were made.</summary>
    public int StatusCalls { get; private set; }

    /// <summary>How many pings were made.</summary>
    public int PingCalls => Volatile.Read(ref _pingCalls);

    private int _pingCalls;

    /// <summary>The cursor each event stream was opened with.</summary>
    public IReadOnlyList<long> StreamCursors { get { lock (_gate) { return [.. _cursors]; } } }

    private readonly List<long> _cursors = [];

    /// <summary>When set, each event stream waits for this task before it records its cursor and opens, so a test can make a stream open late. Null opens at once.</summary>
    public Func<Task>? BeforeStreamOpens { get; set; }

    /// <summary>Appends an event to the ledger and sends it to every open stream.</summary>
    /// <param name="events">The events.</param>
    public void Push(params EventView[] events)
    {
        lock (_gate)
        {
            foreach (var e in events)
            {
                _events.Add(e);
                foreach (var s in _streams)
                {
                    _ = s.Writer.TryWrite(e);
                }
            }
        }
    }

    /// <summary>Ends every open stream, as a dropped connection does.</summary>
    public void Drop()
    {
        lock (_gate)
        {
            foreach (var s in _streams)
            {
                _ = s.Writer.TryComplete();
            }

            _streams.Clear();
        }

    }

    /// <summary>Takes the server down: calls fail and open streams end.</summary>
    public void GoDown()
    {
        Down = true;
        Drop();
    }

    /// <summary>Brings it back.</summary>
    public void ComeUp() => Down = false;

    public Task<Result<PingView>> Ping(Uri api, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _pingCalls);
        if (_pinged is { } last && last != (Pong.Pid, Pong.Epoch))
        {
            _pending = 0;   // another process or epoch is another ledger: a sequence from the old one means nothing
        }

        _pinged = (Pong.Pid, Pong.Epoch);
        return Task.FromResult(Down ? Unreachable<PingView>(api) : Refusal is { } refusal ? Refused<PingView>(refusal) : FailPing ? Unreachable<PingView>(api) : Pong);
    }

    public Task<Result<StatusView>> Status(Uri api, CancellationToken cancellationToken) => Status(api, after: null, cancellationToken);

    /// <summary>
    /// A status call that carries <paramref name="after"/> as X-Sindri-After (0 for none), as the HTTP handler passes the
    /// header it received; null is a direct call, which carries the sequence of the last accepted command.
    /// </summary>
    /// <param name="api">The API.</param>
    /// <param name="after">The sequence the request carried, or null for a direct call.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The answer.</returns>
    internal async Task<Result<StatusView>> Status(Uri api, long? after, CancellationToken cancellationToken)
    {
        StatusCalls++;
        var carried = Carried(after, unreachable: Down || FailStatus);
        _statusAfters.Add(carried);
        if (StatusGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        // The server's order: nothing answers, the caller check (a refusal), then the route's own answers.
        return Down ? Unreachable<StatusView>(api)
            : Refusal is { } refusal ? Refused<StatusView>(refusal)
            : FailStatus ? Unreachable<StatusView>(api)
            : carried > 0 && Lag != ViewsLag.None ? Behind(carried)
            : StatusError is { } failure ? failure
            : UnreadableStatus ? SindriUiErrors.BadAnswer.With(UnreadableReason)
            : Current;
    }

    public async IAsyncEnumerable<EventView> Events(Uri api, long after, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (BeforeStreamOpens is { } gate)
        {
            await gate().WaitAsync(cancellationToken);
        }

        if (Down || StreamsEndAtOnce || Refusal is not null)
        {
            lock (_gate)
            {
                _cursors.Add(after);
            }

            yield break;
        }

        var stream = Channel.CreateUnbounded<EventView>();
        lock (_gate)
        {
            _cursors.Add(after);
            foreach (var e in _events.Where(e => e.Sequence > after))
            {
                _ = stream.Writer.TryWrite(e);
            }

            _streams.Add(stream);
        }

        await foreach (var e in stream.Reader.ReadAllAsync(cancellationToken))
        {
            yield return e;
        }
    }

    // A view query uses the sequence once, whatever the answer is (best effort, UI spec §9.1), but not when the call never
    // got one (a transport failure leaves it for the next read, as the client does). A request over HTTP carries its own.
    private long Carried(long? after, bool unreachable) =>
        after ?? (unreachable ? Volatile.Read(ref _pending) : Interlocked.Exchange(ref _pending, 0));

    private static Error Behind(long sequence) =>
        new("SND-API-011", ErrorKind.Unavailable, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"The views have not reached sequence {sequence} (fix: retry shortly)"));

    // The client's reading of a 403 ProblemDetails: Sindri's code and detail as they are (API §3.6, UI spec §2).
    private static Result<T> Refused<T>((string Code, string Detail) refusal) => new Error(refusal.Code, ErrorKind.Forbidden, refusal.Detail);

    private static Result<T> Unreachable<T>(Uri api) => SindriUiErrors.Unreachable.With($"nothing answers at {api.Authority}");
}
