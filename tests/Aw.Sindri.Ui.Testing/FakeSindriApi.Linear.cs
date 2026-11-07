using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Ui.Api.Linear;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The Linear reads and writes of the fake (Linear hub spec §3, 1b contract): each answers the API's goldens, the same text the handler serves, and records what the app sent.</summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<LinearIssueQuery> _linearQueries = [];
    private readonly List<string?> _linearSinceAfters = [];
    private readonly List<string> _linearLookups = [];
    private readonly List<LinearChangeSent> _linearChanges = [];
    private readonly List<LinearCommentSent> _linearComments = [];
    private readonly List<LinearCreateBody> _linearCreates = [];
    private readonly List<string> _linearTeamKeys = [];
    private readonly List<LinearConnectBody> _linearConnects = [];
    private readonly Queue<RouteAnswer> _linearChangeQueue = [];
    private readonly Queue<RouteAnswer> _linearCommentQueue = [];
    private readonly Queue<RouteAnswer> _linearCreateQueue = [];
    private int _linearDisconnects;

    /// <summary>GET /sindri/linear/issues's answer: the "mine" golden by default.</summary>
    public RouteAnswer LinearIssuesAnswer { get; set; } = RouteAnswer.Api("linear-issues", "mine");

    /// <summary>When set, chooses the list's answer per query (the group, the cursor, the filters); <see cref="LinearIssuesAnswer"/> answers every query otherwise.</summary>
    public Func<LinearIssueQuery, RouteAnswer>? LinearIssuesFor { get; set; }

    /// <summary>GET /sindri/linear/issues/{key}'s answer: the "full" golden by default.</summary>
    public RouteAnswer LinearIssueAnswer { get; set; } = RouteAnswer.Api("linear-issue", "full");

    /// <summary>GET /sindri/linear/lookups/{team}'s answer: the "eng" golden by default.</summary>
    public RouteAnswer LinearLookupsAnswer { get; set; } = RouteAnswer.Api("linear-lookups", "eng");

    /// <summary>GET /sindri/linear/since's answer: the "three" golden by default.</summary>
    public RouteAnswer LinearSinceAnswer { get; set; } = RouteAnswer.Api("linear-since", "three");

    /// <summary>When set, the lookups read waits for it.</summary>
    public TaskCompletionSource? LinearLookupsGate { get; set; }

    /// <summary>When set, the since read waits for it (after it has recorded its marker).</summary>
    public TaskCompletionSource? LinearSinceGate { get; set; }

    /// <summary>The list queries the app sent, in order.</summary>
    public IReadOnlyList<LinearIssueQuery> LinearQueries { get { lock (_gate) { return [.. _linearQueries]; } } }

    /// <summary>The markers the app sent to the since route, in order (null for none).</summary>
    public IReadOnlyList<string?> LinearSinceAfters { get { lock (_gate) { return [.. _linearSinceAfters]; } } }

    /// <summary>The lookups reads the app made, by team key, in order.</summary>
    public IReadOnlyList<string> LinearLookupsRead { get { lock (_gate) { return [.. _linearLookups]; } } }

    public Task<Result<LinearIssueList>> LinearIssues(Uri api, LinearIssueQuery query, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _linearQueries.Add(query);
        }

        return Task.FromResult(Answer<LinearIssueList>(api, LinearRoutes.IssueList(query), LinearIssuesFor?.Invoke(query) ?? LinearIssuesAnswer));
    }

    public Task<Result<LinearIssueOne>> LinearIssue(Uri api, string key, CancellationToken cancellationToken) =>
        Task.FromResult(Answer<LinearIssueOne>(api, LinearRoutes.Issue(key), LinearIssueAnswer));

    public async Task<Result<LinearLookups>> LinearLookups(Uri api, string team, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _linearLookups.Add(team);
        }

        await WaitFor(LinearLookupsGate, cancellationToken).ConfigureAwait(false);
        return Answer<LinearLookups>(api, LinearRoutes.Lookups(team), LinearLookupsAnswer);
    }

    public async Task<Result<LinearSince>> LinearSince(Uri api, string? after, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _linearSinceAfters.Add(after);
        }

        var answer = LinearSinceAnswer;   // what was served when the read was asked
        await WaitFor(LinearSinceGate, cancellationToken).ConfigureAwait(false);
        return Answer<LinearSince>(api, LinearRoutes.Since(after), answer);
    }

    /// <summary>POST /sindri/linear/issues/{key}/change's answer once the queue is empty: the "applied" golden by default.</summary>
    public RouteAnswer LinearChangeAnswer { get; set; } = RouteAnswer.Api("linear-change", "applied");

    /// <summary>POST /sindri/linear/issues/{key}/comments' answer once the queue is empty: the "applied" golden by default.</summary>
    public RouteAnswer LinearCommentAnswer { get; set; } = RouteAnswer.Api("linear-comment", "applied");

    /// <summary>POST /sindri/linear/issues' answer once the queue is empty: the "applied" golden by default.</summary>
    public RouteAnswer LinearCreateAnswer { get; set; } = RouteAnswer.Api("linear-create", "applied");

    /// <summary>GET /sindri/linear/connection's answer: the "connected" golden by default.</summary>
    public RouteAnswer LinearConnectionAnswer { get; set; } = RouteAnswer.Api("linear-connection", "connected");

    /// <summary>POST /sindri/linear/connection/teams' answer: the "two-teams" golden by default.</summary>
    public RouteAnswer LinearConnectionTeamsAnswer { get; set; } = RouteAnswer.Api("linear-connection-teams", "two-teams");

    /// <summary>POST /sindri/linear/connection's answer: the "read-saved-approved" golden by default.</summary>
    public RouteAnswer LinearConnectAnswer { get; set; } = RouteAnswer.Api("linear-connect", "read-saved-approved");

    /// <summary>DELETE /sindri/linear/connection/write's answer: the "removed" golden by default.</summary>
    public RouteAnswer LinearDisconnectWriteAnswer { get; set; } = RouteAnswer.Api("linear-disconnect-write", "removed");

    /// <summary>When set, a change waits for it (after it has recorded its body).</summary>
    public TaskCompletionSource? LinearChangeGate { get; set; }

    /// <summary>When set, a comment waits for it (after it has recorded its body).</summary>
    public TaskCompletionSource? LinearCommentGate { get; set; }

    /// <summary>When set, a create waits for it (after it has recorded its body).</summary>
    public TaskCompletionSource? LinearCreateGate { get; set; }

    /// <summary>When set, the teams read waits for it (after it has recorded the call).</summary>
    public TaskCompletionSource? LinearConnectionTeamsGate { get; set; }

    /// <summary>When set, a connection save waits for it (after it has recorded its body).</summary>
    public TaskCompletionSource? LinearConnectGate { get; set; }

    /// <summary>The edits the app sent, in order.</summary>
    public IReadOnlyList<LinearChangeSent> LinearChanges { get { lock (_gate) { return [.. _linearChanges]; } } }

    /// <summary>The comments the app sent, in order.</summary>
    public IReadOnlyList<LinearCommentSent> LinearComments { get { lock (_gate) { return [.. _linearComments]; } } }

    /// <summary>The new issues the app sent, in order.</summary>
    public IReadOnlyList<LinearCreateBody> LinearCreates { get { lock (_gate) { return [.. _linearCreates]; } } }

    /// <summary>The read keys the app asked the teams of, in order.</summary>
    public IReadOnlyList<string> LinearTeamKeys { get { lock (_gate) { return [.. _linearTeamKeys]; } } }

    /// <summary>The connection saves the app sent, in order.</summary>
    public IReadOnlyList<LinearConnectBody> LinearConnects { get { lock (_gate) { return [.. _linearConnects]; } } }

    /// <summary>How many times the app removed the write key.</summary>
    public int LinearDisconnects => Volatile.Read(ref _linearDisconnects);

    /// <summary>Answers the next edits in this order before <see cref="LinearChangeAnswer"/> (a conflict, then the retry's applied).</summary>
    /// <param name="answers">The answers.</param>
    public void QueueLinearChange(params RouteAnswer[] answers) => Enqueue(_linearChangeQueue, answers);

    /// <summary>Answers the next comments in this order before <see cref="LinearCommentAnswer"/>.</summary>
    /// <param name="answers">The answers.</param>
    public void QueueLinearComment(params RouteAnswer[] answers) => Enqueue(_linearCommentQueue, answers);

    /// <summary>Answers the next creates in this order before <see cref="LinearCreateAnswer"/>.</summary>
    /// <param name="answers">The answers.</param>
    public void QueueLinearCreate(params RouteAnswer[] answers) => Enqueue(_linearCreateQueue, answers);

    public async Task<Result<LinearWriteOutcome>> LinearChange(Uri api, string key, JsonObject opened, JsonObject set, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _linearChanges.Add(new LinearChangeSent(key, new LinearChangeBody(opened, set)));
        }

        var answer = Next(_linearChangeQueue, LinearChangeAnswer);
        await WaitFor(LinearChangeGate, cancellationToken).ConfigureAwait(false);
        return LinearWriteOutcomes.Of(Answer<LinearChangeApplied>(api, LinearRoutes.Change(key), answer));
    }

    public async Task<Result<LinearWriteOutcome>> LinearComment(Uri api, string key, string clientId, string body, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _linearComments.Add(new LinearCommentSent(key, clientId, body));
        }

        var answer = Next(_linearCommentQueue, LinearCommentAnswer);
        await WaitFor(LinearCommentGate, cancellationToken).ConfigureAwait(false);
        return LinearWriteOutcomes.Of(Answer<LinearCommentPosted>(api, LinearRoutes.Comments(key), answer));
    }

    public async Task<Result<LinearWriteOutcome>> LinearCreate(Uri api, LinearCreateBody body, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _linearCreates.Add(body);
        }

        var answer = Next(_linearCreateQueue, LinearCreateAnswer);
        await WaitFor(LinearCreateGate, cancellationToken).ConfigureAwait(false);
        return LinearWriteOutcomes.Of(Answer<LinearCreated>(api, LinearRoutes.Issues, answer));
    }

    public Task<Result<LinearConnection>> LinearConnection(Uri api, CancellationToken cancellationToken) =>
        Task.FromResult(Answer<LinearConnection>(api, LinearRoutes.Connection, LinearConnectionAnswer));

    public async Task<Result<LinearTeamChoices>> LinearConnectionTeams(Uri api, string key, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _linearTeamKeys.Add(key);
        }

        await WaitFor(LinearConnectionTeamsGate, cancellationToken).ConfigureAwait(false);
        return Answer<LinearTeamChoices>(api, LinearRoutes.ConnectionTeams, LinearConnectionTeamsAnswer);
    }

    public async Task<Result<LinearConnected>> LinearConnect(Uri api, string kind, string key, IReadOnlyList<string>? teams, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _linearConnects.Add(new LinearConnectBody(kind, key, teams));
        }

        await WaitFor(LinearConnectGate, cancellationToken).ConfigureAwait(false);
        return Answer<LinearConnected>(api, LinearRoutes.Connection, LinearConnectAnswer);
    }

    public Task<Result<LinearDisconnected>> LinearDisconnectWrite(Uri api, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _linearDisconnects);
        return Task.FromResult(Answer<LinearDisconnected>(api, LinearRoutes.ConnectionWrite, LinearDisconnectWriteAnswer));
    }

    private void Enqueue(Queue<RouteAnswer> queue, RouteAnswer[] answers)
    {
        lock (_gate)
        {
            foreach (var answer in answers)
            {
                queue.Enqueue(answer);
            }
        }
    }

    // The next queued answer, or the settable one when none is queued; taken when the call was made, as the since read does.
    private RouteAnswer Next(Queue<RouteAnswer> queue, RouteAnswer fallback)
    {
        lock (_gate)
        {
            return queue.Count > 0 ? queue.Dequeue() : fallback;
        }
    }
}
