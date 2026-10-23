using Aw.Kernel;
using Aw.Sindri.Ui.Api.Sessions;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The session reads of the fake (collector spec CA-1, CA-2): each answers the API's goldens (<see cref="SessionAnswers"/>), the same text the handler serves.</summary>
internal sealed partial class FakeSindriApi
{
    /// <summary>When set, both session reads answer this instead of the golden (a 503 problem, an odd body).</summary>
    public RouteAnswer? SessionAnswer { get; set; }

    /// <summary>What GET /sessions answers.</summary>
    /// <returns>The answer.</returns>
    internal RouteAnswer SessionListAnswer() => SessionAnswer ?? SessionAnswers.List();

    /// <summary>What GET /sessions/{provider}/{id} answers.</summary>
    /// <param name="provider">The provider.</param>
    /// <param name="id">The session id.</param>
    /// <returns>The answer.</returns>
    internal RouteAnswer SessionOneAnswer(string provider, string id) => SessionOneBody?.Invoke(provider, id) ?? SessionAnswer ?? SessionAnswers.One(provider, id);

    /// <summary>When set, answers a one-session read before the shared answer does; return null to fall through.</summary>
    public Func<string, string, RouteAnswer?>? SessionOneBody { get; set; }

    private int _sessionsCalls;
    private int _sessionCalls;

    /// <summary>How many times the session list was read.</summary>
    public int SessionsCalls => Volatile.Read(ref _sessionsCalls);

    /// <summary>How many times one session was read.</summary>
    public int SessionCalls => Volatile.Read(ref _sessionCalls);

    /// <summary>The query of the latest list read; null before the first.</summary>
    public SessionQuery? LastSessionsQuery { get; private set; }

    /// <summary>The ref (provider/id) of the latest one-session read; null before the first.</summary>
    public string? LastSessionRef { get; private set; }

    /// <summary>When set, the list read waits on it before answering (the page shows its reading state meanwhile).</summary>
    public TaskCompletionSource? SessionsGate { get; set; }

    public async Task<Result<SessionList>> Sessions(Uri api, SessionQuery query, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _sessionsCalls);
        LastSessionsQuery = query;
        await WaitFor(SessionsGate, cancellationToken).ConfigureAwait(false);
        return Answer<SessionList>(api, SessionRoutes.List(query), SessionListAnswer());
    }

    public Task<Result<SessionView>> Session(Uri api, string provider, string id, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _sessionCalls);
        LastSessionRef = provider + "/" + id;
        return Task.FromResult(Answer<SessionView>(api, SessionRoutes.One(provider, id), SessionOneAnswer(provider, id)));
    }
}
