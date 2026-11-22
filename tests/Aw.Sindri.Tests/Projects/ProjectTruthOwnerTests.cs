using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Projects.Cards;
using Aw.Sindri.Core.Projects.Truth;
using Aw.Sindri.Core.Projects.Vocabulary;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.ListReviews;
using Aw.Sindri.Features.ProjectFacts;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.Projects;

/// <summary>The owner's GitHub login on the page's truth (issue 3797): read once per page from the Reviews read's source, and a page that knows no login says so.</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectTruthOwnerTests
{
    private readonly ViewsHarness _v = new();
    private readonly CountingAccount _account = new(new GitHubAccount(GitHubAccountState.Connected, "github:work", "joi-example"));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ReviewPull Pull(string repo, int number, params string[] requested) =>
        new(repo, number, "PR", "sam", Draft: false, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, "abc", requested, [], ReviewersRead: true, [], $"https://github.com/{repo}/pull/{number.ToString(CultureInfo.InvariantCulture)}");

    private static ProjectCard Card(string key, string link) =>
        new(key, ProjectOrigin.HandMade, key, Description: null, ProjectState.InProgress, StateSetByOwner: false, Health: null, Lead: null, Target: null, OpenIssues: null, Counting: false,
            OpenPrs: null, PrScope: null, LastActivity: null, FinishedAt: null, [], [new ProjectLink(link, link)], Notes: null, Synced: true, Version: 1);

    private static ProjectsPage Page() => new(Empty: null, Viewer: null, [], [Card("prj_a", "https://github.com/acme/api"), Card("prj_b", "https://github.com/acme/web")], [], []);

    private async Task<SindriProjectTruthFacts> Facts(params ReviewPull[] pulls)
    {
        _ = (await _v.Views.Open(ViewSchema.Version, Token)).Value;
        var reviews = new FakeReviewFacts().With(new ReviewFactsSnapshot(pulls, Github: null, new ReviewLibraryFacts(Reading: false, Docs: 0, WithErrors: 0, UnreadableRoots: [])));
        return new SindriProjectTruthFacts(reviews, new NoSummaries(), _v.Views, new InMemorySessionSnapshot(), _v.Slice.Profiles, new ReviewAccountReader(_account, new FakePullRequests(["github.com"])));
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> NoProfileRepos => new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    [Fact]
    public async Task A_page_of_two_projects_reads_the_owner_login_once_and_marks_both_blocked_on_you()
    {
        var facts = await Facts(Pull("acme/api", 1, "Joi-Example"), Pull("acme/web", 2, "joi-example"));

        var page = await ProjectTruthPage.Attach(Page(), NoProfileRepos, facts, Token);

        Assert.Equal(1, _account.Reads);
        Assert.All(page.Projects, static c => Assert.True(c.Truth.BlockedOnYou));
        Assert.DoesNotContain(page.Freshness, static r => r.Source == "github-login");
    }

    [Fact]
    public async Task With_github_not_connected_nothing_is_blocked_on_you_and_the_page_says_the_login_is_not_known()
    {
        _account.Account = new GitHubAccount(GitHubAccountState.Missing, Connection: null, Login: null);
        var facts = await Facts(Pull("acme/api", 1, "joi-example"));

        var page = await ProjectTruthPage.Attach(Page(), NoProfileRepos, facts, Token);

        Assert.All(page.Projects, static c => Assert.False(c.Truth.BlockedOnYou));
        Assert.Equal(ProjectTruthPage.NoLogin, Assert.Single(page.Freshness, static r => r.Source == "github-login"));
    }

    private sealed class CountingAccount(GitHubAccount account) : IGitHubAccount
    {
        internal GitHubAccount Account { get; set; } = account;

        internal int Reads { get; private set; }

        public ValueTask<Result<GitHubAccount>> Current(CancellationToken cancellationToken)
        {
            Reads++;
            return ValueTask.FromResult(Results.Success(Account));
        }
    }

    private sealed class NoSummaries : IReviewSummaries
    {
        public ValueTask<Result<ReviewSummary?>> Read(ReviewPullRef pull, string sha, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success<ReviewSummary?>(value: null));

        public ValueTask<Result<ReviewSummary?>> Newest(ReviewPullRef pull, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success<ReviewSummary?>(value: null));

        public ValueTask<Result<Unit>> Write(ReviewPullRef pull, ReviewSummary summary, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success(Unit.Value));
    }
}
