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

    public Task<Result<LinearLookups>> LinearLookups(Uri api, string team, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _linearLookups.Add(team);
        }

        return Task.FromResult(Answer<LinearLookups>(api, LinearRoutes.Lookups(team), LinearLookupsAnswer));
    }

    public Task<Result<LinearSince>> LinearSince(Uri api, string? after, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _linearSinceAfters.Add(after);
        }

        return Task.FromResult(Answer<LinearSince>(api, LinearRoutes.Since(after), LinearSinceAnswer));
    }
}
