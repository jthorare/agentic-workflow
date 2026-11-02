using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Connectors.Core;
using Aw.Connectors.Core.GitHub;
using Aw.Kernel;

namespace Aw.Connectors.Infrastructure.GitHub;

/// <summary>
/// Reads a repo's issue list (pull requests included), its comments and a pull request's detail from the GitHub REST API (owned index spec §5.3): GET /repos/{o}/{r}/issues oldest change first, from the
/// cursor's instant (inclusive), a page at a time by the Link header; GET .../issues/comments repo-wide; GET .../pulls/{n} and .../pulls/{n}/files. A rate limit stops the run and comes back as a value with the reset time (never a sleep). The host rules
/// are checked before the first request; a redirect is refused; the token goes only in the Authorization header and in no error.
/// </summary>
internal sealed partial class GitHubClient : IGitHubReader
{
    private const string DefaultRoot = "https://api.github.com";
    private const int DefaultWaitSeconds = 60;
    private const int MaxFilePages = 3;
    private const int MaxIssueCommentPages = 10;
    private const string TimeFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    private readonly HttpClient _http;
    private readonly GitHubClientSettings _settings;
    private readonly IHostResolver _resolver;
    private readonly TimeProvider _clock;

    /// <summary>Creates the client.</summary>
    /// <param name="http">The client; its handler must not follow redirects (see <see cref="GitHubHttp.CreateHandler"/>).</param>
    /// <param name="settings">The host, the private-host flag and the token.</param>
    /// <param name="resolver">Resolves the host name for the host rules.</param>
    /// <param name="clock">The clock a rate-limit wait is counted from.</param>
    public GitHubClient(HttpClient http, GitHubClientSettings settings, IHostResolver resolver, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(clock);
        _http = http;
        _settings = settings;
        _resolver = resolver;
        _clock = clock;
    }

    /// <inheritdoc />
    public async ValueTask<Result<GitHubPullPage>> Issues(string repo, GitHubCursor after, int limit, DateTimeOffset? before, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(after);
        var root = await Prepare(repo, cancellationToken).ConfigureAwait(false);
        return root.IsError ? root.Error : await ReadIssues(FirstIssues(root.Value, repo, after), after, limit, before, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<Result<GitHubCommentPage>> Comments(string repo, DateTimeOffset since, DateTimeOffset until, CancellationToken cancellationToken)
    {
        var root = await Prepare(repo, cancellationToken).ConfigureAwait(false);
        if (root.IsError)
        {
            return root.Error;
        }

        var from = Uri.EscapeDataString(since.UtcDateTime.ToString(TimeFormat, CultureInfo.InvariantCulture));
        var url = new Uri($"{Base(root.Value)}/repos/{repo}/issues/comments?sort=updated&direction=asc&since={from}&per_page=100");
        return await ReadComments(url, until, maxPages: int.MaxValue, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<Result<GitHubCommentPage>> IssueComments(string repo, int number, CancellationToken cancellationToken)
    {
        var root = await Prepare(repo, cancellationToken).ConfigureAwait(false);
        if (root.IsError)
        {
            return root.Error;
        }

        var url = new Uri(string.Create(CultureInfo.InvariantCulture, $"{Base(root.Value)}/repos/{repo}/issues/{number}/comments?per_page=100"));
        return await ReadComments(url, DateTimeOffset.MaxValue, MaxIssueCommentPages, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<Result<GitHubPullDetailRead>> PullRequest(string repo, int number, CancellationToken cancellationToken)
    {
        var root = await Prepare(repo, cancellationToken).ConfigureAwait(false);
        if (root.IsError)
        {
            return root.Error;
        }

        var pull = new Uri(string.Create(CultureInfo.InvariantCulture, $"{Base(root.Value)}/repos/{repo}/pulls/{number}"));
        var head = await Fetch(pull, cancellationToken).ConfigureAwait(false);
        if (head.IsError)
        {
            return head.Error;
        }

        if (head.Value.RateLimitedUntil is { } limited)
        {
            return new GitHubPullDetailRead(Detail: null, limited);
        }

        if (head.Value.Body is not { ValueKind: JsonValueKind.Object } detail)
        {
            return ConnectorsErrors.BadReply.With("GitHub's pull request was not a JSON object.");
        }

        var paths = await ReadPaths(pull, cancellationToken).ConfigureAwait(false);
        return paths.IsError
            ? paths.Error
            : paths.Value.RateLimitedUntil is { } until
                ? new GitHubPullDetailRead(Detail: null, until)
                : new GitHubPullDetailRead(GitHubWire.ReadPull(detail, paths.Value.Paths), RateLimitedUntil: null);
    }

    private async ValueTask<Result<(List<string> Paths, DateTimeOffset? RateLimitedUntil)>> ReadPaths(Uri pull, CancellationToken cancellationToken)
    {
        List<string> paths = [];
        Uri? files = new(pull.AbsoluteUri + "/files?per_page=100");
        for (var page = 0; files is not null && page < MaxFilePages; page++)
        {
            var step = await Fetch(files, cancellationToken).ConfigureAwait(false);
            if (step.IsError)
            {
                return step.Error;
            }

            if (step.Value.RateLimitedUntil is { } until)
            {
                return (paths, until);
            }

            if (step.Value.Body is not { ValueKind: JsonValueKind.Array } array)
            {
                return ConnectorsErrors.BadReply.With("GitHub's file list was not a JSON array.");
            }

            paths.AddRange(array.EnumerateArray().Select(GitHubWire.ReadPath).OfType<string>());
            files = step.Value.Next;
        }

        return (paths, null);
    }

    private async ValueTask<Result<Uri>> Prepare(string repo, CancellationToken cancellationToken)
    {
        if (repo is null || !RepoName.IsMatch(repo))
        {
            return ConnectorsErrors.Invalid.With("The repo must be 'owner/name'.");
        }

        if (_settings.Token is { Length: > 0 } token && token.Any(static c => char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            return ConnectorsErrors.Invalid.With("The GitHub token has whitespace or control characters; point the profile at the token itself.");
        }

        var root = Root();
        if (root.IsError)
        {
            return root.Error;
        }

        return await CheckHost(root.Value, cancellationToken).ConfigureAwait(false) is { } refused ? refused : root.Value;
    }

    private static string Base(Uri root) => root.AbsoluteUri.TrimEnd('/');

    private Result<Uri> Root()
    {
        if (string.IsNullOrWhiteSpace(_settings.Host))
        {
            return new Uri(DefaultRoot);
        }

        var host = _settings.Host.Trim();
        if (host.Any(static c => char.IsWhiteSpace(c) || c is '/' or '\\' or '@' or '?' or '#')
            || !Uri.TryCreate("https://" + host, UriKind.Absolute, out var parsed) || parsed.AbsolutePath != "/")
        {
            return ConnectorsErrors.Invalid.With("context.sources.github.host must be a host name with an optional port (no scheme, path or user).");
        }

        return new Uri($"https://{parsed.Authority}/api/v3");
    }

    private async ValueTask<Error?> CheckHost(Uri root, CancellationToken cancellationToken)
    {
        if (_settings.AllowPrivateHost)
        {
            return null;
        }

        if (root.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6)
        {
            return ConnectorsErrors.HostRefused.With($"{root.Host} is an IP literal; set context.sources.github.allowPrivateHost if that is intended.");
        }

        IReadOnlyList<IPAddress> addresses;
        try
        {
            addresses = await _resolver.Resolve(root.DnsSafeHost, cancellationToken).ConfigureAwait(false);
        }
        catch (SocketException)
        {
            return ConnectorsErrors.Unreachable.With($"{root.DnsSafeHost} did not resolve.");
        }

        if (addresses.Count == 0)
        {
            return ConnectorsErrors.Unreachable.With($"{root.DnsSafeHost} did not resolve.");
        }

        return addresses.All(HostPolicy.IsPublic)
            ? null
            : ConnectorsErrors.HostRefused.With($"{root.DnsSafeHost} resolves to a private, loopback or link-local address; set context.sources.github.allowPrivateHost if that is intended.");
    }

    // The list is oldest change first, so the first entry that is not before the bound ends the read.
    private static bool Before(GitHubIssue issue, DateTimeOffset? bound) => bound is not { } end || issue.UpdatedAt < end;

    private static Uri FirstIssues(Uri root, string repo, GitHubCursor after)
    {
        var since = Uri.EscapeDataString(after.UpdatedAt.UtcDateTime.ToString(TimeFormat, CultureInfo.InvariantCulture));
        return new($"{Base(root)}/repos/{repo}/issues?state=all&sort=updated&direction=asc&since={since}&per_page=100");
    }

    private async ValueTask<Result<GitHubPullPage>> ReadIssues(Uri first, GitHubCursor after, int limit, DateTimeOffset? before, CancellationToken cancellationToken)
    {
        var url = first;
        List<GitHubIssue> items = [];
        while (url is not null)
        {
            var step = await Fetch(url, cancellationToken).ConfigureAwait(false);
            if (step.IsError)
            {
                return step.Error;
            }

            if (step.Value.RateLimitedUntil is { } until)
            {
                return new GitHubPullPage(items, after.Advance(items), RateLimitedUntil: until);
            }

            if (step.Value.Body is not { ValueKind: JsonValueKind.Array } array)
            {
                return ConnectorsErrors.BadReply.With("GitHub's issue list was not a JSON array.");
            }

            var read = array.EnumerateArray().Select(GitHubWire.ReadIssue).ToList();
            if (read.Contains(null))
            {
                return ConnectorsErrors.BadReply.With("An entry of GitHub's issue list lacked a field the connector reads.");
            }

            var listed = read.OfType<GitHubIssue>().ToList();
            items.AddRange(listed.Where(i => after.Admits(i) && Before(i, before)));
            url = listed.Exists(i => !Before(i, before)) || items.Count >= limit ? null : step.Value.Next;
        }

        return new GitHubPullPage(items, after.Advance(items), RateLimitedUntil: null);
    }

    private async ValueTask<Result<GitHubCommentPage>> ReadComments(Uri first, DateTimeOffset until, int maxPages, CancellationToken cancellationToken)
    {
        List<GitHubComment> items = [];
        var url = first;
        for (var page = 0; url is not null && page < maxPages; page++)
        {
            var step = await Fetch(url, cancellationToken).ConfigureAwait(false);
            if (step.IsError)
            {
                return step.Error;
            }

            if (step.Value.RateLimitedUntil is { } limited)
            {
                return new GitHubCommentPage(items, limited);
            }

            if (step.Value.Body is not { ValueKind: JsonValueKind.Array } array)
            {
                return ConnectorsErrors.BadReply.With("GitHub's comment list was not a JSON array.");
            }

            var read = array.EnumerateArray().Select(GitHubWire.ReadComment).ToList();
            if (read.Contains(null))
            {
                return ConnectorsErrors.BadReply.With("An entry of GitHub's comment list lacked a field the connector reads.");
            }

            var comments = read.OfType<GitHubComment>().ToList();
            items.AddRange(comments.Where(c => c.UpdatedAt <= until));
            url = comments.Exists(c => c.UpdatedAt > until) ? null : step.Value.Next;
        }

        return new GitHubCommentPage(items, RateLimitedUntil: null);
    }

    [GeneratedRegex("^(?!\\.{1,2}/)(?!.*/\\.{1,2}$)[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex RepoName { get; }
}
