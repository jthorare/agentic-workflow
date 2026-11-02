using System.Net;
using Aw.Connectors.Core.GitHub;
using Aw.Connectors.Infrastructure.GitHub;
using Aw.Connectors.Testing.GitHub;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Connectors.Tests.GitHub;

public sealed class GitHubClientContractTests : GitHubReaderContract, IDisposable
{
    private readonly FakeGitHubServer _server = new();

    public void Dispose() => _server.Dispose();

    protected override IGitHubReader Reader(IReadOnlyList<GitHubIssue> issues, IReadOnlyList<GitHubComment> comments, IReadOnlyDictionary<int, GitHubPullDetail> pulls)
    {
        foreach (var issue in issues)
        {
            _ = _server.Add(Repo, issue);
        }

        foreach (var comment in comments)
        {
            _ = _server.AddComment(Repo, comment);
        }

        foreach (var (number, detail) in pulls)
        {
            _ = _server.AddPull(Repo, number, detail);
        }

        return new GitHubClient(
            new HttpClient(_server),
            new GitHubClientSettings(),
            new PublicResolver(),
            new FakeTimeProvider(new DateTimeOffset(2026, 10, 27, 9, 0, 0, TimeSpan.Zero)));
    }

    private sealed class PublicResolver : IHostResolver
    {
        public ValueTask<IReadOnlyList<IPAddress>> Resolve(string host, CancellationToken cancellationToken)
            => ValueTask.FromResult<IReadOnlyList<IPAddress>>([IPAddress.Parse("140.82.112.5")]);
    }
}
