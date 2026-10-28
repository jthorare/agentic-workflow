using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Connectors.Core;
using Aw.Connectors.Core.GitHub;

namespace Aw.Connectors.Features.PullGitHub;

/// <summary>
/// Maps a GitHub issue or pull request onto a <see cref="ConnectorItem"/> (owned index spec §5.3): its record key, cleaned text with its comments folded in (newest-capped, oldest first),
/// permalink, state (open, closed, merged or draft) and links: 'closes' for a pull request's closing keywords, 'relates' for a '#n' in a comment (and in a pull request's own body),
/// 'touches' ('path:owner/name/path') for each file a pull request changed. A '#n' names an issue key, since the list does not say whether n is an issue or a pull request.
/// </summary>
internal static partial class GitHubRecords
{
    private const int MaxPaths = 300;

    public static ConnectorItem From(string repo, GitHubIssue issue, IReadOnlyList<GitHubComment> comments, GitHubPullDetail? detail)
    {
        var title = OneLine(TextCleaner.Clean(issue.Title));
        var body = TextCleaner.Clean(issue.Body);
        var talk = Talk(comments);
        var text = string.Join("\n\n", new[] { title, body, talk }.Where(static part => part.Length > 0));
        return new ConnectorItem(
            Key(repo, issue.IsPullRequest, issue.Number),
            issue.IsPullRequest ? "pr" : "issue",
            title,
            text,
            issue.Author,
            Permalink(issue.Url),
            State(issue, detail),
            issue.State,
            issue.CreatedAt,
            issue.UpdatedAt,
            issue.ClosedAt,
            issue.UpdatedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            Links(repo, issue, body, comments, detail),
            repo);
    }

    private static string State(GitHubIssue issue, GitHubPullDetail? detail)
    {
        if (detail is { Merged: true })
        {
            return "merged";
        }

        if (string.Equals(issue.State, "closed", StringComparison.Ordinal))
        {
            return "closed";
        }

        return detail is { Draft: true } ? "draft" : "open";
    }

    private static string Talk(IReadOnlyList<GitHubComment> comments)
        => string.Join(
            "\n\n",
            comments.Select(static c => (c.Author, Text: TextCleaner.Clean(c.Body))).Where(static c => c.Text.Length > 0).Select(static c => $"{c.Author ?? "ghost"}: {c.Text}"));

    private static List<ConnectorLink> Links(string repo, GitHubIssue issue, string cleanedBody, IReadOnlyList<GitHubComment> comments, GitHubPullDetail? detail)
    {
        var closes = issue.IsPullRequest ? Closes(issue.Number, issue.Body) : [];
        var mentioned = new SortedSet<int>(comments.SelectMany(c => References(issue.Number, TextCleaner.Clean(c.Body))));
        if (issue.IsPullRequest)
        {
            mentioned.UnionWith(References(issue.Number, cleanedBody));
        }

        mentioned.ExceptWith(closes);
        return
        [
            .. closes.Select(n => new ConnectorLink("closes", Key(repo, pullRequest: false, n))),
            .. mentioned.Select(n => new ConnectorLink("relates", Key(repo, pullRequest: false, n))),
            .. (detail?.Paths ?? []).Where(IsSafePath).Distinct(StringComparer.Ordinal).Take(MaxPaths).Select(path => new ConnectorLink("touches", $"path:{repo}/{path}")),
        ];
    }

    private static bool IsSafePath(string path)
        => path.Length > 0 && path[0] != '/' && !path.Contains('\\', StringComparison.Ordinal) && !path.Any(char.IsControl)
            && !path.Split('/').Any(static segment => segment.Length == 0 || string.Equals(segment, ".", StringComparison.Ordinal) || string.Equals(segment, "..", StringComparison.Ordinal));

    private static string Key(string repo, bool pullRequest, int number)
        => string.Create(CultureInfo.InvariantCulture, $"github:{(pullRequest ? "pr" : "issue")}/{repo}/{number}");

    private static string? Permalink(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && uri.UserInfo.Length == 0 ? uri.AbsoluteUri : null;

    private static string OneLine(string text) => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static SortedSet<int> Closes(int own, string body) => Numbers(ClosingReference.Matches(body), own);

    private static SortedSet<int> References(int own, string cleaned) => Numbers(Reference.Matches(cleaned), own);

    private static SortedSet<int> Numbers(MatchCollection matches, int own)
    {
        var numbers = new SortedSet<int>();
        foreach (Match match in matches)
        {
            if (int.TryParse(match.Groups["n"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n != own)
            {
                _ = numbers.Add(n);
            }
        }

        return numbers;
    }

    [GeneratedRegex(@"\b(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?)\s*:?\s+#(?<n>\d{1,9})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ClosingReference { get; }

    [GeneratedRegex(@"(?<![\w/#&])#(?<n>\d{1,9})\b", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Reference { get; }
}
