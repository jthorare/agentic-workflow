using System.Text.RegularExpressions;

namespace Aw.Context.Core.Trust;

/// <summary>
/// Which permalinks a result may carry (spec §7): 'https' on linear.app, github.com issue and pull request links, and
/// a Slack workspace's message links (`&lt;workspace&gt;.slack.com/archives/&lt;channel&gt;/p&lt;ts&gt;`); anything else is dropped, never rewritten.
/// </summary>
public static partial class Permalinks
{
    [GeneratedRegex(@"^/[^/]+/[^/]+/(issues|pull)/\d+/?$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 250)]
    private static partial Regex GithubRecordPath { get; }

    [GeneratedRegex(@"^/archives/[A-Z0-9]+/p\d+/?$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 250)]
    private static partial Regex SlackMessagePath { get; }

    /// <summary>The link when it is allowed, else null.</summary>
    /// <param name="link">A source's permalink, or null.</param>
    /// <returns>The link or null.</returns>
    public static Uri? Allowed(Uri? link)
    {
        if (link is not { IsAbsoluteUri: true } || !string.Equals(link.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) || !link.IsDefaultPort)
        {
            return null;
        }

        var host = link.IdnHost.ToLowerInvariant();
        var path = link.AbsolutePath;
        var ok = host == "linear.app"
            || (host == "github.com" && GithubRecordPath.IsMatch(path))
            || (IsWorkspaceSlackHost(host) && SlackMessagePath.IsMatch(path));
        return ok ? link : null;
    }

    private static bool IsWorkspaceSlackHost(string host) =>
        host.EndsWith(".slack.com", StringComparison.Ordinal) && host.Length > ".slack.com".Length && !host[..^".slack.com".Length].Contains('.', StringComparison.Ordinal);
}
