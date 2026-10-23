using System.Text.RegularExpressions;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The repository key T3 Code gives a project (its 'repositoryIdentity.canonicalKey'), computed the same way from the repo's remotes: the primary
/// remote is 'upstream', else 'origin', else the first by name, and its URL is normalized to 'host/path' (T3's normalizeGitRemoteUrl).
/// </summary>
internal static partial class T3RemoteKeys
{
    /// <summary>The key of the primary remote among <paramref name="remotes"/> (name → fetch URL), or null when there is none.</summary>
    /// <param name="remotes">The remotes.</param>
    /// <returns>The key, or null.</returns>
    public static string? Primary(IReadOnlyDictionary<string, string> remotes)
    {
        ArgumentNullException.ThrowIfNull(remotes);
        foreach (var name in (string[])["upstream", "origin"])
        {
            if (remotes.TryGetValue(name, out var url))
            {
                return Normalize(url);
            }
        }

        return remotes.OrderBy(static r => r.Key, StringComparer.Ordinal).Select(static r => Normalize(r.Value)).FirstOrDefault();
    }

    /// <summary>A remote URL as 'host/path': no scheme, user, '.git' or trailing slash, lower case.</summary>
    /// <param name="url">The remote URL ('git@github.com:Owner/Name.git', 'https://github.com/owner/name').</param>
    /// <returns>The key.</returns>
    public static string Normalize(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        var normalized = TrailingSlashes.Replace(url.Trim(), string.Empty);
        normalized = (normalized.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? normalized[..^4] : normalized).ToLowerInvariant();
        if (Schemed.IsMatch(normalized) && Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments.Length > 1 ? Azure(uri.Host, segments) ?? $"{uri.Host}/{string.Join('/', segments)}" : normalized;
        }

        if (!DriveLetter.IsMatch(normalized) && ScpStyle.Match(normalized) is { Success: true } scp)
        {
            var host = scp.Groups["host"].Value;
            var path = scp.Groups["path"].Value;
            return Azure(host, path.Split('/')) ?? $"{host}/{path}";
        }

        return normalized;
    }

    /// <summary>Azure DevOps' SSH spelling ('ssh.dev.azure.com', 'v3/org/project/repo') as its web one, as T3 does.</summary>
    private static string? Azure(string host, string[] segments)
    {
        if ((host != "ssh.dev.azure.com" && host != "vs-ssh.visualstudio.com") || segments is not ["v3", var organization, var project, var repository])
        {
            return null;
        }

        return host == "ssh.dev.azure.com" ? $"dev.azure.com/{organization}/{project}/_git/{repository}" : $"{organization}.visualstudio.com/{project}/_git/{repository}";
    }

    [GeneratedRegex("/+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TrailingSlashes { get; }

    [GeneratedRegex("^(?:ssh|https?|git)://", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Schemed { get; }

    [GeneratedRegex("^[a-z]:", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DriveLetter { get; }

    [GeneratedRegex(@"^(?:[a-zA-Z0-9._-]+@)?(?<host>[^@:/\\\s]+):/?(?<path>[^/\s]+(?:/[^/\s]+)+)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ScpStyle { get; }
}
