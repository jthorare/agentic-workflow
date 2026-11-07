using Aw.Kernel;
using Aw.Sindri.Ui.Api.Linear;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The Linear reads of the fake (Linear hub spec §3): each answers the API's goldens, the same text the handler serves.</summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<LinearIssueQuery> _linearQueries = [];

    /// <summary>GET /sindri/linear/issues's answer: the "mine" golden by default.</summary>
    public RouteAnswer LinearIssuesAnswer { get; set; } = RouteAnswer.Api("linear-issues", "mine");

    /// <summary>GET /sindri/linear/issues/{key}'s answer: the "full" golden by default.</summary>
    public RouteAnswer LinearIssueAnswer { get; set; } = RouteAnswer.Api("linear-issue", "full");

    /// <summary>GET /sindri/linear/lookups/{team}'s answer: the "eng" golden by default.</summary>
    public RouteAnswer LinearLookupsAnswer { get; set; } = RouteAnswer.Api("linear-lookups", "eng");

    /// <summary>GET /sindri/linear/since's answer: the "three" golden by default.</summary>
    public RouteAnswer LinearSinceAnswer { get; set; } = RouteAnswer.Api("linear-since", "three");

    /// <summary>The list queries the app sent, in order.</summary>
    public IReadOnlyList<LinearIssueQuery> LinearQueries { get { lock (_gate) { return [.. _linearQueries]; } } }

    public Task<Result<LinearIssueList>> LinearIssues(Uri api, LinearIssueQuery query, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _linearQueries.Add(query);
        }

        return Task.FromResult(Answer<LinearIssueList>(api, LinearRoutes.IssueList(query), LinearIssuesAnswer));
    }

    public Task<Result<LinearIssueOne>> LinearIssue(Uri api, string key, CancellationToken cancellationToken) =>
        Task.FromResult(Answer<LinearIssueOne>(api, LinearRoutes.Issue(key), LinearIssueAnswer));

    public Task<Result<LinearLookups>> LinearLookups(Uri api, string team, CancellationToken cancellationToken) =>
        Task.FromResult(Answer<LinearLookups>(api, LinearRoutes.Lookups(team), LinearLookupsAnswer));

    public Task<Result<LinearSince>> LinearSince(Uri api, string? after, CancellationToken cancellationToken) =>
        Task.FromResult(Answer<LinearSince>(api, LinearRoutes.Since(after), LinearSinceAnswer));
}
