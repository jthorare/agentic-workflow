using System.Globalization;
using Aw.Connectors.Core.GitHub;
using Aw.Connectors.Features;
using Aw.Connectors.Testing;
using Aw.Connectors.Testing.GitHub;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Testing.Records;
using Aw.Kernel;
using Aw.Sindri.Host.IndexSources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.IndexSources;

/// <summary>
/// One repo with twelve pull requests, four to a page, behind <see cref="FakeGitHubReader"/>, read by the real GitHub connector through Sindri's <see cref="ConnectorFeed"/>. It answers like GitHub's rate limit (an empty answer with a reset time)
/// once <see cref="ResumeUpstream.InterruptAfter"/> has run out, for the list, the comment reads and the pull request reads alike.
/// </summary>
internal sealed class GitHubResumeUpstream : ResumeUpstream, IGitHubReader
{
    private const string Repo = "acme/widgets";
    private static readonly DateTimeOffset _now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _resetsAt = new(2026, 10, 27, 13, 0, 0, TimeSpan.Zero);
    private readonly FakeGitHubReader _inner = new() { PageSize = 4 };
    private readonly List<string> _keys = [];

    public GitHubResumeUpstream()
    {
        for (var number = 1; number <= 12; number++)
        {
            _ = _inner.Add(Repo, A.GitHubIssue(number).AsPullRequest().UpdatedAt(_now.AddDays(-10).AddMinutes(number)));
            _keys.Add(string.Create(CultureInfo.InvariantCulture, $"github:pr/{Repo}/{number}"));
        }

        using var services = new ServiceCollection().AddSingleton<TimeProvider>(new FakeTimeProvider(_now)).AddGitHubConnectorFactory().BuildServiceProvider();
        var settings = GitHubSettings.Create([Repo], token: null, host: null, allowPrivateHost: false, commentCap: null).Value;
        var connector = services.GetRequiredService<IGitHubConnectorFactory>().Create(settings, this, TimeSpan.FromDays(30));
        Feed = new ConnectorFeed(SourceId.From("github"), connector, new ProfileRepoMap([Repo], [ContextRepoName.From("widgets")]).Place);
    }

    public override RecordFeed Feed { get; }

    public override IReadOnlyCollection<string> Keys => _keys;

    public ValueTask<Result<GitHubPullPage>> Issues(string repo, GitHubCursor after, int limit, DateTimeOffset? before, CancellationToken cancellationToken) =>
        Admit(string.Create(CultureInfo.InvariantCulture, $"issues {repo} {after.UpdatedAt.UtcTicks}|{string.Join(',', after.SeenIds)} {limit}"))
            ? _inner.Issues(repo, after, limit, before, cancellationToken)
            : ValueTask.FromResult<Result<GitHubPullPage>>(new GitHubPullPage([], after, _resetsAt));

    public ValueTask<Result<GitHubCommentPage>> Comments(string repo, DateTimeOffset since, DateTimeOffset until, CancellationToken cancellationToken) =>
        Admit("comments " + repo) ? _inner.Comments(repo, since, until, cancellationToken) : ValueTask.FromResult<Result<GitHubCommentPage>>(new GitHubCommentPage([], _resetsAt));

    public ValueTask<Result<GitHubCommentPage>> IssueComments(string repo, int number, CancellationToken cancellationToken) =>
        Admit(string.Create(CultureInfo.InvariantCulture, $"comments {repo}#{number}"))
            ? _inner.IssueComments(repo, number, cancellationToken)
            : ValueTask.FromResult<Result<GitHubCommentPage>>(new GitHubCommentPage([], _resetsAt));

    public ValueTask<Result<GitHubPullDetailRead>> PullRequest(string repo, int number, CancellationToken cancellationToken) =>
        Admit(string.Create(CultureInfo.InvariantCulture, $"pull {repo}#{number}"))
            ? _inner.PullRequest(repo, number, cancellationToken)
            : ValueTask.FromResult<Result<GitHubPullDetailRead>>(new GitHubPullDetailRead(Detail: null, _resetsAt));
}
