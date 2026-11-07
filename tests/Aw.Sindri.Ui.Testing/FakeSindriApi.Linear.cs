using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Ui.Api.Linear;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The Linear reads of the fake (Linear hub spec §3): each answers the API's goldens, the same text the handler serves.</summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<LinearIssueQuery> _linearQueries = [];
    private readonly List<string?> _linearSinceAfters = [];
    private readonly List<string> _linearLookups = [];

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

    public Task<Result<LinearWriteOutcome>> LinearChange(Uri api, string key, JsonObject opened, JsonObject set, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<Result<LinearWriteOutcome>> LinearComment(Uri api, string key, string clientId, string body, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<Result<LinearWriteOutcome>> LinearCreate(Uri api, LinearCreateBody body, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<Result<LinearConnection>> LinearConnection(Uri api, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<Result<LinearTeamChoices>> LinearConnectionTeams(Uri api, string key, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<Result<LinearConnected>> LinearConnect(Uri api, string kind, string key, IReadOnlyList<string>? teams, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<Result<LinearDisconnected>> LinearDisconnectWrite(Uri api, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
