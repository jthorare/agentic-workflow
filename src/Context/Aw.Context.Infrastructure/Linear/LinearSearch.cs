using System.Diagnostics;
using System.Text.RegularExpressions;
using Aw.Context.Core;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>
/// One search of the linear source (spec §5.2): title and key lookup. An issue identifier among the terms is looked up by key; documents, issues and projects are matched on their title
/// or name (project updates have none and are listed in their project's record). The query's UpdatedSince goes to Linear as an 'updatedAt' filter and the profile's teams as a team
/// filter, both before the cap, so the profile's records are found however many other projects the workspace has. Each kind is paged until the query's limit. Every call gets what is left of
/// the query's deadline. Hits come in groups (key lookups, documents, projects, issues), each newest change first, deduplicated by canonical key and cut to the limit.
/// </summary>
/// <param name="client">The client.</param>
/// <param name="scope">The profile's teams.</param>
/// <param name="clock">When Linear answered.</param>
internal sealed partial class LinearSearch(LinearClient client, LinearScope scope, TimeProvider clock)
{
    private sealed record Plan(List<string> Terms, DateTimeOffset? Since, int Want);

    private const int MaxTerms = 32;
    private const int MaxKeys = 10;
    private const int MinPerKind = 50;
    private const int MaxPerKind = 1000;

    /// <summary>Searches.</summary>
    /// <param name="query">The terms, cut-off, limit and deadline.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The hits in rank order, or the first error.</returns>
    public async ValueTask<Result<IReadOnlyList<SourceHit>>> Run(SourceQuery query, CancellationToken cancellationToken)
    {
        var terms = query.Terms.Select(t => t.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxTerms).ToList();
        if (terms.Count == 0 || query.Limit <= 0)
        {
            return Array.Empty<SourceHit>();
        }

        var started = Stopwatch.GetTimestamp();
        TimeSpan Left() => query.Deadline - Stopwatch.GetElapsedTime(started);
        var plan = new Plan(terms, query.UpdatedSince, Math.Clamp(query.Limit, MinPerKind, MaxPerKind));
        var hits = new LinearHits(scope, clock.GetUtcNow(), query.AsOf);
        var all = new List<SourceHit>();
        foreach (var step in new Func<ValueTask<Result<List<SourceHit>>>>[]
        {
            () => Keys(terms, hits, Left, cancellationToken),
            () => Documents(plan, hits, Left, cancellationToken),
            () => Projects(plan, hits, Left, cancellationToken),
            () => Issues(plan, hits, Left, cancellationToken),
        })
        {
            var group = await step().ConfigureAwait(false);
            if (group.IsError)
            {
                return group.Error;
            }

            all.AddRange(group.Value);
        }

        return all.DistinctBy(h => h.Ref.Canonical.Value, StringComparer.Ordinal).Take(query.Limit).Select((h, i) => h with { Rank = i + 1 }).ToList();
    }

    private async ValueTask<Result<List<SourceHit>>> Keys(List<string> terms, LinearHits hits, Func<TimeSpan> left, CancellationToken cancellationToken)
    {
        var found = new List<SourceHit>();
        foreach (var key in terms.Where(k => IsIdentifier(k) && scope.Reads(k[..k.IndexOf('-', StringComparison.Ordinal)])).Take(MaxKeys))
        {
            var issue = await client.Query<LinearIssueData>("Issue", LinearFilters.ById(key), left(), cancellationToken).ConfigureAwait(false);
            if (issue.IsError && issue.Error.Code != ContextErrors.LinearRecordNotFound.Code)
            {
                return issue.Error;
            }

            if (issue.IsSuccess && issue.Value.Issue is { } node && scope.Reads(node.Team?.Key) && hits.Of(node) is { } hit)
            {
                found.Add(hit);
            }
        }

        return found;
    }

    private async ValueTask<Result<List<SourceHit>>> Documents(Plan plan, LinearHits hits, Func<TimeSpan> left, CancellationToken cancellationToken)
    {
        var filter = LinearFilters.Matching("title", plan.Terms, plan.Since);
        if (scope.IsScoped)
        {
            _ = LinearFilters.InProjectsOfTeams(filter, scope.Teams);
        }

        var data = await client.Pages(new LinearList<LinearDocumentsData, LinearDocument>("DocumentHits", filter, plan.Want, static d => d.Documents), left, cancellationToken).ConfigureAwait(false);
        return data.IsError ? data.Error : Newest(data.Value, d => d.UpdatedAt, hits.Of);
    }

    private async ValueTask<Result<List<SourceHit>>> Projects(Plan plan, LinearHits hits, Func<TimeSpan> left, CancellationToken cancellationToken)
    {
        var filter = LinearFilters.Matching("name", plan.Terms, plan.Since);
        if (scope.IsScoped)
        {
            _ = LinearFilters.OfTeams(filter, scope.Teams);
        }

        var data = await client.Pages(new LinearList<LinearProjectsData, LinearProject>("Projects", filter, plan.Want, static d => d.Projects), left, cancellationToken).ConfigureAwait(false);
        return data.IsError ? data.Error : Newest(data.Value, p => p.UpdatedAt, hits.Of);
    }

    private async ValueTask<Result<List<SourceHit>>> Issues(Plan plan, LinearHits hits, Func<TimeSpan> left, CancellationToken cancellationToken)
    {
        var filter = LinearFilters.Matching("title", plan.Terms, plan.Since);
        if (scope.IsScoped)
        {
            _ = LinearFilters.InTeams(filter, scope.Teams);
        }

        var data = await client.Pages(new LinearList<LinearIssuesData, LinearIssue>("IssueHits", filter, plan.Want, static d => d.Issues), left, cancellationToken).ConfigureAwait(false);
        return data.IsError ? data.Error : Newest(data.Value, i => i.UpdatedAt, hits.Of);
    }

    private static List<SourceHit> Newest<T>(IEnumerable<T> nodes, Func<T, DateTimeOffset> updatedAt, Func<T, SourceHit?> hit) =>
        [.. nodes.OrderByDescending(updatedAt).Select(hit).OfType<SourceHit>()];

    private static bool IsIdentifier(string term) => Identifier.IsMatch(term);

    [GeneratedRegex(@"^[A-Z][A-Z0-9]+-[0-9]+\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Identifier { get; }
}
