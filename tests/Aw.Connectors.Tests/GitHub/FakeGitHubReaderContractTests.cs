using Aw.Connectors.Core.GitHub;
using Aw.Connectors.Testing.GitHub;

namespace Aw.Connectors.Tests.GitHub;

public sealed class FakeGitHubReaderContractTests : GitHubReaderContract
{
    protected override IGitHubReader Reader(IReadOnlyList<GitHubIssue> issues, IReadOnlyList<GitHubComment> comments, IReadOnlyDictionary<int, GitHubPullDetail> pulls)
    {
        var fake = new FakeGitHubReader();
        foreach (var issue in issues)
        {
            _ = fake.Add(Repo, issue);
        }

        foreach (var comment in comments)
        {
            _ = fake.AddComment(Repo, comment);
        }

        foreach (var (number, detail) in pulls)
        {
            _ = fake.AddPull(Repo, number, detail);
        }

        return fake;
    }
}
