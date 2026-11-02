using System.Globalization;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>
/// An issue as the record feed hands it over: its text is the description, the newest 20 comments (where decisions live), the project and the relations, as <see cref="LinearText"/> joins them; Linear's state type normalised and its own word kept, the closing time, the team as the audience, and the parent and relations as links.
/// Its GitHub pull request attachments link to the PR records, and its identifier is a 'known-as' link a PR naming it reaches it through (projects page spec 2026-10-29 §4.5).
/// </summary>
internal static class LinearIssueItem
{
    private const string Prefix = "linear:issue/";
    private const string ProjectPrefix = "linear:project/";
    private const string KeyPrefix = "linear:issue-key/";

    // why: a replay with the same version writes nothing, so issues stored before they carried a project link must change version to be re-saved once with it; bump this when the feed's output changes shape
    private const string FeedFormat = ";f2";

    /// <summary>The item for an issue.</summary>
    /// <param name="issue">The issue.</param>
    /// <param name="githubRepos">The profile's GitHub repos ('owner/name'): only their pull request attachments become links.</param>
    /// <returns>The item.</returns>
    public static RecordItem Of(LinearIssue issue, IReadOnlyList<string> githubRepos)
    {
        ArgumentNullException.ThrowIfNull(issue);
        return new RecordItem(
            RecordKey.From(Prefix + issue.Id), RecordKind.Issue, issue.Title, LinearText.Of(issue), issue.Creator?.Name, issue.Url, State(issue.State?.Type), issue.State?.Name,
            issue.CreatedAt, issue.UpdatedAt, issue.CompletedAt ?? issue.CanceledAt, SourceVersion.From(issue.UpdatedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) + FeedFormat),
            Links(issue, githubRepos), Repo: null, issue.Team is { } team && AudienceKey.TryFrom("linear:" + team.Key, out var audience) ? audience : null);
    }

    private static RecordState? State(string? type) => type switch
    {
        "completed" => RecordState.Done,
        "canceled" => RecordState.Canceled,
        "triage" or "backlog" or "unstarted" or "started" => RecordState.Open,
        _ => null,
    };

    private static List<RecordLink> Links(LinearIssue issue, IReadOnlyList<string> githubRepos)
    {
        var links = new List<RecordLink> { new(KeyPrefix + issue.Identifier, RecordLinkKind.KnownAs, Title: null) };
        if (issue.Project is { } project)
        {
            links.Add(new RecordLink(ProjectPrefix + project.Id, RecordLinkKind.InProject, project.Name));
        }

        if (issue.Parent is { } parent)
        {
            links.Add(Link(parent, RecordLinkKind.Parent));
        }

        links.AddRange(Outgoing(issue));
        links.AddRange(Incoming(issue));
        links.AddRange((issue.Attachments?.Nodes ?? []).Select(a => PullRequest(a.Url, githubRepos)).OfType<string>().Select(static key => new RecordLink(key, RecordLinkKind.Relates, Title: null)));
        return [.. links.DistinctBy(static l => (l.ToRef, l.Kind))];
    }

    private static IEnumerable<RecordLink> Outgoing(LinearIssue issue) =>
        (issue.Relations?.Nodes ?? []).SelectMany(static r => r.RelatedIssue is { } other && RelationKind(r.Type) is { } kind ? [Link(other, kind)] : Array.Empty<RecordLink>());

    // why: an inverse 'blocks' or 'duplicate' says the other issue blocks or duplicates this one, which the link kinds (this record blocks the other) have no word for; only 'related' reads the same from both sides
    private static IEnumerable<RecordLink> Incoming(LinearIssue issue) =>
        (issue.InverseRelations?.Nodes ?? []).SelectMany(static r => r.Issue is { } other && string.Equals(r.Type, "related", StringComparison.Ordinal) ? [Link(other, RecordLinkKind.Relates)] : Array.Empty<RecordLink>());

    private static RecordLinkKind? RelationKind(string type) => type switch
    {
        "blocks" => RecordLinkKind.Blocks,
        "duplicate" => RecordLinkKind.Duplicates,
        "related" => RecordLinkKind.Relates,
        _ => null,
    };

    // why: an attachment url is the issue author's text; only an exact 'https://github.com/<o>/<r>/pull/<n>[/...]' of a listed repo becomes a key, spelled as the profile lists the repo so it equals the GitHub feed's key
    private static string? PullRequest(string url, IReadOnlyList<string> githubRepos)
    {
        if (!OnGitHub(url, out var path))
        {
            return null;
        }

        var parts = path.Split('/');
        if (parts.Length < 5 || parts[0].Length > 0 || !string.Equals(parts[3], "pull", StringComparison.Ordinal)
            || !int.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number <= 0
            || githubRepos.FirstOrDefault(r => string.Equals(r, parts[1] + "/" + parts[2], StringComparison.OrdinalIgnoreCase)) is not { } repo)
        {
            return null;
        }

        return string.Create(CultureInfo.InvariantCulture, $"github:pr/{repo}/{number}");
    }

    private static bool OnGitHub(string url, out string path)
    {
        var ok = Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && string.Equals(uri.Host, "github.com", StringComparison.Ordinal)
            && uri.IsDefaultPort && uri.UserInfo.Length == 0;
        path = ok && uri is not null ? uri.AbsolutePath : string.Empty;
        return path.Length > 0;
    }

    private static RecordLink Link(LinearIssueRef other, RecordLinkKind kind) => new(Prefix + (other.Id ?? other.Identifier), kind, other.Title);
}
