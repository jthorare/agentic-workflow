using System.Globalization;
using System.IO.Pipelines;
using System.Net;
using System.Text;

namespace Aw.Sindri.Tests.Commands;

/// <summary>
/// The control plane for the job and service verbs: canned answers by 'METHOD /path' (a list answers in turn, the last repeating), the status
/// the read-your-writes verbs ask for first, and an event stream the test writes to.
/// </summary>
internal sealed class JobApi : HttpMessageHandler
{
    private readonly Pipe _stream = new();
    private readonly Dictionary<string, Reply[]> _replies = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _served = new(StringComparer.Ordinal);
    private readonly List<string> _calls = [];
    private readonly List<string?> _afters = [];
    private readonly Dictionary<string, TaskCompletionSource> _gates = new(StringComparer.Ordinal);
    private readonly List<(Func<bool> Condition, TaskCompletionSource Met)> _waiting = [];
    private int _streams;

    /// <summary>Every call made, as 'METHOD /path?query'.</summary>
    public IReadOnlyList<string> Calls
    {
        get
        {
            lock (_calls)
            {
                return [.. _calls];
            }
        }
    }

    public int Streams => Volatile.Read(ref _streams);

    public string? StreamQuery { get; private set; }

    /// <summary>The event stream is full (429, SND-API-012).</summary>
    public bool StreamsFull { get; init; }

    /// <summary>The event stream is refused with this status and code instead (not a full list).</summary>
    public (HttpStatusCode Status, string Code)? StreamRefused { get; init; }

    /// <summary>The 'X-Sindri-After' header of each GET of the job route, in order (null when the call sent none).</summary>
    public IReadOnlyList<string?> JobAfters
    {
        get
        {
            lock (_calls)
            {
                return [.. _afters];
            }
        }
    }

    /// <summary>Holds the answer to 'METHOD /path' until the returned source is completed: the request is in flight.</summary>
    /// <param name="call">The call, as 'METHOD /path'.</param>
    /// <returns>The source to complete.</returns>
    public TaskCompletionSource Hold(string call)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _gates[call] = gate;
        return gate;
    }

    /// <summary>Every call fails as a stopped server does.</summary>
    public bool Down { get; set; }

    /// <summary>A task that completes once <paramref name="condition"/> holds, checked now and after each call the verb makes (no polling); bounded so a hang fails.</summary>
    /// <param name="condition">The condition, over this handler's counters.</param>
    /// <returns>The task.</returns>
    public Task Until(Func<bool> condition)
    {
        Task met;
        lock (_waiting)
        {
            if (condition())
            {
                return Task.CompletedTask;
            }

            var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiting.Add((condition, source));
            met = source.Task;
        }

        return met.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
    }

    public JobApi On(string method, string path, params Reply[] replies)
    {
        _replies[method + " " + path] = replies;
        return this;
    }

    public JobApi Ok(string method, string path, params string[] bodies) => On(method, path, [.. bodies.Select(static b => new Reply(HttpStatusCode.OK, b))]);

    public int Count(string call) => Calls.Count(c => string.Equals(c, call, StringComparison.Ordinal));

    public async Task Frame(long id, string type, string payload) =>
        _ = await _stream.Writer.WriteAsync(Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"event: {type}\nid: {id}\ndata: {Event(id, type, payload)}\n\n")), TestContext.Current.CancellationToken);

    public async Task End() => await _stream.Writer.CompleteAsync();

    public static string Event(long id, string type, string payload) =>
        string.Create(CultureInfo.InvariantCulture, $$"""{"sequence":{{id}},"id":"00000000-0000-0000-0000-{{id:D12}}","type":"{{type}}","schemaVersion":1,"occurredAt":"2026-01-01T00:00:00.0000000Z","actor":"service:jobs","tenant":"local","ring":0,"item":null,"correlationId":"00000000-0000-0000-0000-000000000009","causationId":null,"traceId":null,"payload":{{payload}}}""");

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;   // why: HttpClient always sets the URI
        Record(request, uri);
        if (uri.AbsolutePath == "/sindri/events")
        {
            _ = Interlocked.Increment(ref _streams);
            StreamQuery = uri.PathAndQuery;
            Notify();
            return OpenStream();
        }

        Notify();
        var key = request.Method.Method + " " + uri.AbsolutePath;
        if (_gates.TryGetValue(key, out var gate))
        {
            await gate.Task.WaitAsync(cancellationToken);
        }

        return Down
            ? throw new HttpRequestException("connection refused")
            : uri.AbsolutePath == "/sindri/status"
                ? Answer(new Reply(HttpStatusCode.OK, FollowRig.Status(withProblem: false)))
                : _replies.TryGetValue(key, out var replies) ? Answer(Next(key, replies)) : Problem(HttpStatusCode.NotFound, "SND-TEST-404");
    }

    private void Record(HttpRequestMessage request, Uri uri)
    {
        lock (_calls)
        {
            _calls.Add(request.Method.Method + " " + uri.PathAndQuery);
            if (request.Method == HttpMethod.Get && uri.AbsolutePath.StartsWith("/sindri/jobs/", StringComparison.Ordinal))
            {
                _afters.Add(request.Headers.TryGetValues("X-Sindri-After", out var after) ? string.Join(',', after) : null);
            }
        }
    }

    private HttpResponseMessage OpenStream() => StreamRefused is { } refused
        ? Problem(refused.Status, refused.Code)
        : StreamsFull
            ? Problem(HttpStatusCode.TooManyRequests, "SND-API-012")
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(_stream.Reader.AsStream()) { Headers = { ContentType = new("text/event-stream") } } };

    private void Notify()
    {
        lock (_waiting)
        {
            foreach (var (condition, met) in _waiting.Where(static w => w.Condition()).ToList())
            {
                _ = met.TrySetResult();
                _ = _waiting.Remove((condition, met));
            }
        }
    }

    private Reply Next(string key, Reply[] replies)
    {
        lock (_served)
        {
            var n = _served.GetValueOrDefault(key);
            _served[key] = n + 1;
            return replies[Math.Min(n, replies.Length - 1)];
        }
    }

    private static HttpResponseMessage Answer(Reply reply) => new(reply.Status)
    {
        Content = new StringContent(reply.Body, Encoding.UTF8, (int)reply.Status >= 400 ? "application/problem+json" : "application/json"),
    };

    private static HttpResponseMessage Problem(HttpStatusCode status, string code) =>
        Answer(new Reply(status, string.Create(CultureInfo.InvariantCulture, $$"""{"title":"{{code}}","status":{{(int)status}},"detail":"refused"}""")));
}
