using System.Diagnostics.CodeAnalysis;
using Aw.Kernel;
using Aw.Sindri.Features.LinearIssues;

namespace Aw.Sindri.Testing;

/// <summary>
/// The in-memory <see cref="ILinearIssueFacts"/>: a fixed source and a fixed set of stored issues that it filters, orders (newest update first, then record key) and pages as Context's list does.
/// <see cref="VisibleTeams"/> stands for a session's audience rule. Records every read.
/// </summary>
[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "Internal because ILinearIssueFacts is; Aw.Sindri.Tests uses it through InternalsVisibleTo, which MA0182 can't see.")]
internal sealed class FakeLinearIssueFacts : ILinearIssueFacts
{
    private readonly List<(LinearIssueFactsFilter Filter, LinearIssuePosition? After)> _asked = [];
    private Result<LinearSourceFacts> _source = new LinearSourceFacts(Connected: true, Viewer: null, new LinearFeedView("ok", LastSyncedAt: null, Code: null, Reread: null));
    private IReadOnlyList<LinearIssueRowView> _issues = [];
    private IReadOnlyList<string>? _visibleTeams;
    private IReadOnlyList<LinearIssueDetailFacts> _details = [];
    private Error? _failing;
    private int _pageSize = int.MaxValue;

    /// <summary>Every issue read, in order.</summary>
    public IReadOnlyList<(LinearIssueFactsFilter Filter, LinearIssuePosition? After)> Asked => _asked;

    /// <summary>Answers the source read with <paramref name="source"/>.</summary>
    /// <param name="source">The source facts.</param>
    /// <returns>The same instance.</returns>
    public FakeLinearIssueFacts WithSource(LinearSourceFacts source)
    {
        _source = source;
        return this;
    }

    /// <summary>Stores <paramref name="issues"/>.</summary>
    /// <param name="issues">The stored issues, in any order.</param>
    /// <returns>The same instance.</returns>
    public FakeLinearIssueFacts WithIssues(params LinearIssueRowView[] issues)
    {
        _issues = issues;
        return this;
    }

    /// <summary>Stores <paramref name="details"/> for the by-key read.</summary>
    /// <param name="details">The stored issues with their descriptions and comments.</param>
    /// <returns>The same instance.</returns>
    public FakeLinearIssueFacts WithDetails(params LinearIssueDetailFacts[] details)
    {
        _details = details;
        return this;
    }

    /// <summary>Limits the issue reads to these teams, as a session of a repo mapped to them sees only those.</summary>
    /// <param name="teams">Team keys.</param>
    /// <returns>The same instance.</returns>
    public FakeLinearIssueFacts VisibleTeams(params string[] teams)
    {
        _visibleTeams = teams;
        return this;
    }

    /// <summary>Caps a page below the asked limit, as a store with a smaller page would.</summary>
    /// <param name="pageSize">The most issues one read returns.</param>
    /// <returns>The same instance.</returns>
    public FakeLinearIssueFacts WithPageSize(int pageSize)
    {
        _pageSize = pageSize;
        return this;
    }

    /// <summary>Fails every read with <paramref name="error"/>.</summary>
    /// <param name="error">The error.</param>
    /// <returns>The same instance.</returns>
    public FakeLinearIssueFacts Failing(Error error)
    {
        _failing = error;
        _source = error;
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<LinearSourceFacts>> Source(CancellationToken cancellationToken) => ValueTask.FromResult(_source);

    /// <inheritdoc/>
    public ValueTask<Result<LinearIssueFactsPage>> Issues(LinearIssueFactsFilter filter, LinearIssuePosition? after, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        _asked.Add((filter, after));
        if (_failing is { } error)
        {
            return ValueTask.FromResult<Result<LinearIssueFactsPage>>(error);
        }

        var ordered = _issues
            .Where(i => Visible(i) && Narrows(i, filter))
            .OrderByDescending(static i => i.UpdatedAt)
            .ThenBy(static i => i.Record, StringComparer.Ordinal)
            .Where(i => after is null || i.UpdatedAt < after.UpdatedAt || (i.UpdatedAt == after.UpdatedAt && string.CompareOrdinal(i.Record, after.Record) > 0))
            .ToList();
        var size = Math.Min(filter.Limit, _pageSize);
        return ValueTask.FromResult<Result<LinearIssueFactsPage>>(new LinearIssueFactsPage([.. ordered.Take(size)], ordered.Count > size));
    }

    /// <inheritdoc/>
    public ValueTask<Result<LinearIssueDetailFacts?>> Detail(string key, CancellationToken cancellationToken)
    {
        if (_failing is { } error)
        {
            return ValueTask.FromResult<Result<LinearIssueDetailFacts?>>(error);
        }

        var found = _details.FirstOrDefault(d => string.Equals(d.Row.Key, key, StringComparison.OrdinalIgnoreCase) && Visible(d.Row));
        return ValueTask.FromResult(Results.Success(found));
    }

    private bool Visible(LinearIssueRowView issue) => _visibleTeams is null || _visibleTeams.Contains(issue.Team.Key, StringComparer.Ordinal);

    private static bool Narrows(LinearIssueRowView issue, LinearIssueFactsFilter filter) =>
        (filter.Team is null || string.Equals(issue.Team.Key, filter.Team, StringComparison.Ordinal))
        && (filter.Assignee is null || string.Equals(issue.Assignee?.Id, filter.Assignee, StringComparison.Ordinal))
        && (filter.StateTypes is null || filter.StateTypes.Contains(issue.State.Type, StringComparer.Ordinal))
        && InProjectAndCycle(issue, filter);

    private static bool InProjectAndCycle(LinearIssueRowView issue, LinearIssueFactsFilter filter) =>
        (filter.Project is null || string.Equals(issue.Project?.Id, filter.Project, StringComparison.Ordinal))
        && (filter.Cycle is null || string.Equals(issue.Cycle?.Id, filter.Cycle, StringComparison.Ordinal));
}
