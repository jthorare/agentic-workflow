using System.Text;
using System.Text.RegularExpressions;
using Aw.Context.Core.Trust;

namespace Aw.Context.Core.Extract;

/// <summary>
/// A GitHub repo read from a checkout's origin URL, and the link to one of its files (#1702). Only github.com is read, in the three shapes git
/// prints (scp-like ssh, ssh:// and https://); userinfo is always dropped and owner and name must be plain, so a remote that carries a token
/// or isn't GitHub gives no link, never a changed one.
/// </summary>
public sealed partial class GithubRemote
{
    [GeneratedRegex(@"^(?:git@github\.com:|ssh://git@github\.com/|https://(?:[^/@\s]+@)?github\.com/)(?<owner>[A-Za-z0-9._-]+)/(?<name>[A-Za-z0-9._-]+?)(?:\.git)?/?$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 250)]
    private static partial Regex Origin { get; }

    [GeneratedRegex("^[0-9a-f]{40}(?:[0-9a-f]{24})?$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 250)]
    private static partial Regex Sha { get; }

    private GithubRemote(string owner, string name)
    {
        Owner = owner;
        Name = name;
    }

    /// <summary>The repo's owner.</summary>
    public string Owner { get; }

    /// <summary>The repo's name, without '.git'.</summary>
    public string Name { get; }

    /// <summary>Reads the repo from an origin URL.</summary>
    /// <param name="origin">The URL as git prints it, or null.</param>
    /// <returns>The repo; null when the URL isn't a plain github.com one.</returns>
    public static GithubRemote? Parse(string? origin)
    {
        if (origin is null || Origin.Match(origin.Trim()) is not { Success: true } match)
        {
            return null;
        }

        var owner = match.Groups["owner"].Value;
        var name = match.Groups["name"].Value;
        return IsDots(owner) || IsDots(name) ? null : new GithubRemote(owner, name);
    }

    /// <summary>The link to a file: github.com/&lt;owner&gt;/&lt;name&gt;/blob/&lt;ref&gt;/&lt;path&gt;, each path segment percent-escaped.</summary>
    /// <param name="commit">The commit to pin the link to when it is a full sha; anything else (or null) links HEAD.</param>
    /// <param name="path">The file's path in the repo, '/'-separated.</param>
    /// <param name="egress">The per-field scrubber: a link it would change at all is dropped, never rewritten.</param>
    /// <returns>The link; null when the path is empty or has a '.' or '..' segment, or the scrubber objects.</returns>
    public Uri? Blob(string? commit, string path, IEgressScrubber egress)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(egress);
        var segments = path.Split('/');
        if (segments.Any(s => s.Length == 0 || IsDots(s)))
        {
            return null;
        }

        var link = new StringBuilder("https://github.com/").Append(Owner).Append('/').Append(Name).Append("/blob/")
            .Append(commit is not null && Sha.IsMatch(commit) ? commit : "HEAD");
        foreach (var segment in segments)
        {
            _ = link.Append('/').Append(Uri.EscapeDataString(segment));
        }

        var text = link.ToString();
        return egress.Scrub(text) is { Blocked: false } scrubbed && string.Equals(scrubbed.Text, text, StringComparison.Ordinal) && Uri.TryCreate(text, UriKind.Absolute, out var uri) ? uri : null;
    }

    private static bool IsDots(string segment) => string.Equals(segment, ".", StringComparison.Ordinal) || string.Equals(segment, "..", StringComparison.Ordinal);
}
