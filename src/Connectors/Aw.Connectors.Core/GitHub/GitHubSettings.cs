using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Connectors.Core.GitHub;

/// <summary>
/// The profile's 'context.sources.github' entry, checked (owned index spec §5.3, §5.4): the repos to read, the secret pointer to a read-only token, an optional
/// Enterprise host and the cap on comments read per record. Only <see cref="Create"/> makes one. No message repeats the token.
/// </summary>
public sealed partial record GitHubSettings
{
    /// <summary>The default cap on comments read per record.</summary>
    public const int DefaultCommentCap = 100;

    private const int MaxCommentCap = 1000;
    private const int MaxRepos = 500;
    private static readonly HashSet<string> _schemes = new(StringComparer.Ordinal) { "env", "file", "keychain", "op" };

    private GitHubSettings(IReadOnlyList<string> repos, string? token, string? host, bool allowPrivateHost, int commentCap)
    {
        Repos = repos;
        Token = token;
        Host = host;
        AllowPrivateHost = allowPrivateHost;
        CommentCap = commentCap;
    }

    /// <summary>The repos read, each 'owner/name', without duplicates.</summary>
    public IReadOnlyList<string> Repos { get; }

    /// <summary>The secret pointer to the token ('env:…', 'file:…', 'keychain:…', 'op:…'), never the token itself; null reads unauthenticated.</summary>
    public string? Token { get; }

    /// <summary>An Enterprise API host name (with an optional port), or null for api.github.com.</summary>
    public string? Host { get; }

    /// <summary>True when the profile marks a private, loopback or IP-literal host as intended.</summary>
    public bool AllowPrivateHost { get; }

    /// <summary>The most comments read for one record.</summary>
    public int CommentCap { get; }

    /// <summary>Checks the profile's values.</summary>
    /// <param name="repos">'context.sources.github.repos'.</param>
    /// <param name="token">'context.sources.github.token', a secret pointer, or null.</param>
    /// <param name="host">'context.sources.github.host', or null.</param>
    /// <param name="allowPrivateHost">'context.sources.github.allowPrivateHost'.</param>
    /// <param name="commentCap">'context.sources.github.commentCap', or null for <see cref="DefaultCommentCap"/>.</param>
    /// <returns>The settings, or AW-CONNECTORS-001 saying which key is wrong (a token is never repeated).</returns>
    public static Result<GitHubSettings> Create(IReadOnlyList<string> repos, string? token, string? host, bool allowPrivateHost, int? commentCap)
    {
        ArgumentNullException.ThrowIfNull(repos);
        if (repos.Count is 0 or > MaxRepos)
        {
            return Wrong("repos", $"list between 1 and {MaxRepos} repos, each 'owner/name'");
        }

        if (repos.FirstOrDefault(static r => !RepoName.IsMatch(r)) is not null)
        {
            return Wrong("repos", "every entry must be 'owner/name' (letters, digits, '-', '_' and '.')");
        }

        if (token is not null && !IsPointer(token))
        {
            return Wrong("token", "this isn't a secret pointer, so it isn't used or shown here; keep the token outside the profile and point to it with env:NAME, file:/path (mode 0600), keychain:service/account or op:vault/item/field");
        }

        if (host is not null && !HostName.IsMatch(host))
        {
            return Wrong("host", "give a host name with an optional port, no scheme, path or user name");
        }

        var cap = commentCap ?? DefaultCommentCap;
        return cap is < 0 or > MaxCommentCap
            ? Wrong("commentCap", $"a number from 0 to {MaxCommentCap}")
            : new GitHubSettings([.. repos.Distinct(StringComparer.OrdinalIgnoreCase)], token, host, allowPrivateHost, cap);
    }

    /// <summary>The settings without the token pointer.</summary>
    /// <returns>A description.</returns>
    public override string ToString() => $"GitHubSettings {{ Repos = {string.Join(',', Repos)}, Host = {Host}, CommentCap = {CommentCap}, Token = (withheld) }}";

    private static Error Wrong(string key, string expected)
        => ConnectorsErrors.Invalid.With($"context.sources.github.{key}: {expected}.");

    private static bool IsPointer(string token)
    {
        var colon = token.IndexOf(':', StringComparison.Ordinal);
        return colon > 0 && colon < token.Length - 1 && _schemes.Contains(token[..colon]);
    }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]*/[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RepoName { get; }

    [GeneratedRegex(@"^[A-Za-z0-9]([A-Za-z0-9.-]*[A-Za-z0-9])?(:\d{1,5})?$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HostName { get; }
}
