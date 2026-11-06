using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>
/// Re-reads one issue the way the feed reads a page (Linear hub spec 2026-11-04 D4): the 'Issue' operation selects what 'IssuesPull' selects, and <see cref="LinearIssueItem.Of"/> maps it, so the record a write-through
/// stores is the record the next tick would store for the same version.
/// </summary>
/// <param name="client">The Linear client.</param>
/// <param name="deadline">How long the call gets.</param>
/// <param name="githubRepos">The profile's GitHub repos ('owner/name'), whose pull request attachments become links.</param>
internal sealed class LinearIssueReader(LinearClient client, TimeSpan deadline, IReadOnlyList<string> githubRepos)
{
    /// <summary>Reads the issue.</summary>
    /// <param name="issueId">Linear's id of the issue (or its identifier, which Linear resolves too).</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The item, or null when Linear has no such issue or it is not in one of the profile's teams; the source's error when Linear can't be asked.</returns>
    public async ValueTask<Result<RecordItem?>> Read(ExternalId issueId, CancellationToken cancellationToken)
    {
        var data = await client.Query<LinearIssueData>("Issue", LinearFilters.ById(issueId.Value), deadline, cancellationToken).ConfigureAwait(false);
        if (data.IsError)
        {
            return data.Error;
        }

        return Results.Success<RecordItem?>(data.Value.Issue is { } issue && Listed(issue) ? LinearIssueItem.Of(issue, githubRepos) : null);
    }

    private bool Listed(LinearIssue issue)
    {
        var teams = client.Settings.Teams;
        return teams.Count == 0 || (issue.Team?.Key is { } key && teams.Contains(key, StringComparer.Ordinal));
    }
}
