using System.Globalization;
using Aw.Connectors.Core.GitHub;
using Aw.Kernel;

namespace Aw.Connectors.Testing.GitHub;

/// <summary>An in-memory <see cref="IGitHubReader"/>: lists what was added per repo oldest change first (comments and pull request details too), skips what the cursor has seen and reads whole pages like the real client (contract-tested against it).</summary>
public sealed class FakeGitHubReader : IGitHubReader
{
    private readonly Dictionary<string, List<GitHubIssue>> _issues = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<GitHubComment>> _comments = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Repo, int Number), GitHubPullDetail> _pulls = [];
    private readonly List<(string Repo, GitHubCursor After, int Limit)> _calls = [];
    private readonly List<string> _reads = [];
    private DateTimeOffset? _commentRateLimit;
    private const int MaxIssueCommentPages = 10;
    private const int MaxFilePages = 3;
    private Error? _failure;

    /// <summary>The calls made, in order.</summary>
    public IReadOnlyList<(string Repo, GitHubCursor After, int Limit)> Calls => _calls;

    /// <summary>The comment and pull request reads made, in order, as 'comments {repo}', 'comments {repo}#{n}' and 'pull {repo}#{n}'.</summary>
    public IReadOnlyList<string> Reads => _reads;

    /// <summary>Gets the page size the fake reads in.</summary>
    public int PageSize { get; init; } = 100;

    /// <summary>Adds an entry.</summary>
    /// <param name="repo">'owner/name'.</param>
    /// <param name="issue">The entry.</param>
    /// <returns>This fake.</returns>
    public FakeGitHubReader Add(string repo, GitHubIssue issue)
    {
        if (!_issues.TryGetValue(repo, out var list))
        {
            list = [];
            _issues[repo] = list;
        }

        list.Add(issue);
        return this;
    }

    /// <summary>Adds a comment.</summary>
    /// <param name="repo">'owner/name'.</param>
    /// <param name="comment">The comment.</param>
    /// <returns>This fake.</returns>
    public FakeGitHubReader AddComment(string repo, GitHubComment comment)
    {
        if (!_comments.TryGetValue(repo, out var list))
        {
            list = [];
            _comments[repo] = list;
        }

        list.Add(comment);
        return this;
    }

    /// <summary>Sets what the pull request read says about a pull request; one without it reads as open, not a draft, no paths.</summary>
    /// <param name="repo">'owner/name'.</param>
    /// <param name="number">The pull request number.</param>
    /// <param name="detail">The detail.</param>
    /// <returns>This fake.</returns>
    public FakeGitHubReader AddPull(string repo, int number, GitHubPullDetail detail)
    {
        _pulls[(repo, number)] = detail;
        return this;
    }

    /// <summary>Makes every repo-wide comment read stop on a rate limit until <paramref name="until"/>, with nothing read.</summary>
    /// <param name="until">When the quota comes back.</param>
    /// <returns>This fake.</returns>
    public FakeGitHubReader RateLimitComments(DateTimeOffset until)
    {
        _commentRateLimit = until;
        return this;
    }

    /// <summary>Makes every read fail with <paramref name="error"/>.</summary>
    /// <param name="error">The error.</param>
    /// <returns>This fake.</returns>
    public FakeGitHubReader Fail(Error error)
    {
        _failure = error;
        return this;
    }

    /// <inheritdoc />
    public ValueTask<Result<GitHubPullPage>> Issues(string repo, GitHubCursor after, int limit, DateTimeOffset? before, CancellationToken cancellationToken)
    {
        _calls.Add((repo, after, limit));
        if (_failure is not null)
        {
            return ValueTask.FromResult<Result<GitHubPullPage>>(_failure);
        }

        var all = (_issues.TryGetValue(repo, out var list) ? list : [])
            .Where(i => after.Admits(i) && (before is not { } end || i.UpdatedAt < end))
            .OrderBy(static i => i.UpdatedAt)
            .ThenBy(static i => i.Id)
            .ToList();
        var taken = new List<GitHubIssue>();
        for (var start = 0; start < all.Count && taken.Count < limit; start += PageSize)
        {
            taken.AddRange(all.Skip(start).Take(PageSize));
        }

        return ValueTask.FromResult<Result<GitHubPullPage>>(new GitHubPullPage(taken, after.Advance(taken), RateLimitedUntil: null));
    }

    /// <inheritdoc />
    public ValueTask<Result<GitHubCommentPage>> Comments(string repo, DateTimeOffset since, DateTimeOffset until, CancellationToken cancellationToken)
    {
        _reads.Add("comments " + repo);
        if (_failure is not null)
        {
            return ValueTask.FromResult<Result<GitHubCommentPage>>(_failure);
        }

        if (_commentRateLimit is { } limited)
        {
            return ValueTask.FromResult<Result<GitHubCommentPage>>(new GitHubCommentPage([], limited));
        }

        var all = (_comments.TryGetValue(repo, out var list) ? list : [])
            .Where(c => c.UpdatedAt >= since)
            .OrderBy(static c => c.UpdatedAt)
            .ThenBy(static c => c.Id)
            .ToList();
        var taken = new List<GitHubComment>();
        for (var start = 0; start < all.Count; start += PageSize)
        {
            var page = all.Skip(start).Take(PageSize).ToList();
            taken.AddRange(page.Where(c => c.UpdatedAt <= until));
            if (page.Exists(c => c.UpdatedAt > until))
            {
                break;
            }
        }

        return ValueTask.FromResult<Result<GitHubCommentPage>>(new GitHubCommentPage(taken, RateLimitedUntil: null));
    }

    /// <inheritdoc />
    public ValueTask<Result<GitHubCommentPage>> IssueComments(string repo, int number, CancellationToken cancellationToken)
    {
        _reads.Add(string.Create(CultureInfo.InvariantCulture, $"comments {repo}#{number}"));
        if (_failure is not null)
        {
            return ValueTask.FromResult<Result<GitHubCommentPage>>(_failure);
        }

        var own = (_comments.TryGetValue(repo, out var list) ? list : [])
            .Where(c => c.IssueNumber == number)
            .OrderBy(static c => c.CreatedAt)
            .ThenBy(static c => c.Id)
            .Take(PageSize * MaxIssueCommentPages)
            .ToList();
        return ValueTask.FromResult<Result<GitHubCommentPage>>(new GitHubCommentPage(own, RateLimitedUntil: null));
    }

    /// <inheritdoc />
    public ValueTask<Result<GitHubPullDetailRead>> PullRequest(string repo, int number, CancellationToken cancellationToken)
    {
        _reads.Add(string.Create(CultureInfo.InvariantCulture, $"pull {repo}#{number}"));
        if (_failure is not null)
        {
            return ValueTask.FromResult<Result<GitHubPullDetailRead>>(_failure);
        }

        var detail = _pulls.TryGetValue((repo, number), out var found) ? found : new GitHubPullDetail(Merged: false, Draft: false, MergedAt: null, []);
        return ValueTask.FromResult<Result<GitHubPullDetailRead>>(new GitHubPullDetailRead(detail with { Paths = [.. detail.Paths.Take(PageSize * MaxFilePages)] }, RateLimitedUntil: null));
    }
}
