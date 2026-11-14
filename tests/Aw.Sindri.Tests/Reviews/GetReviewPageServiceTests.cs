using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.GetReviewPage;
using Aw.Sindri.Infrastructure.Reviews;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Reviews;

/// <summary>The PR page's read (PR review page spec §6.2, §9): the refusals before any forge call, the page built and cached, a head that moves, and the cache answering when the forge can't be reached.</summary>
[Lane(TestLane.Integration)]
public sealed class GetReviewPageServiceTests : IDisposable
{
    private const string Head = "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678";
    private readonly SliceHarness _h = new();
    private readonly TempHome _home = new();
    private readonly FakePullRequests _forge = new(["github.com"]) { SelfLogin = "sindri-agent" };
    private readonly FakeReviewPageStore _store = new FakeReviewPageStore().Syncing("acme/api");
    private readonly FileReviewSummaries _summaries;
    private readonly GetReviewPageService _service;

    public GetReviewPageServiceTests()
    {
        var home = new FakeSindriHome(_home.Path);
        _summaries = new FileReviewSummaries(home);
        _service = new GetReviewPageService(_h.Profiles, _store, new ReviewPageReader(_forge), new ReviewPageFactsReader(_forge, new FakeReviewEvidence(), new Status(), _h.Clock), new ReviewPageCache(new FileReviewPages(home, _h.Clock), _summaries, new Status(), _h.Clock), new FakeGitHubAccount());
        var pull = _forge.Add(new PullRepo("github.com", "acme", "api"), 2071, "feat/x", Head);
        pull.Title = "Add retry budget";
        pull.Author = "sam-dev";
        _forge.SeedFiles(pull.Link, [new PullFile("src/A.cs", PreviousPath: null, PullFileStatus.Modified, Additions: 1, Deletions: 0, "@@ -1 +1 @@\n-a\n+b")], new Dictionary<string, string>(StringComparer.Ordinal));
        pull.Checks = new PullChecks(PullChecksState.Failing, Head, ["ui"], Passed: 0, Pending: 0, Runs: [new PullCheckRun("ui", "actions", "failure", Head)]);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _home.Dispose();

    private async Task Approve(string? login = "joi-example")
    {
        var profile = ProfileRules.Check(new SindriProfile(1, "thor", Review: login is null ? null : new PrReviewSettings(login))).Value;
        _ = await _h.Profiles.Save(new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml"), Token);
    }

    private ValueTask<Result<ReviewPageModel>> Read(string owner = "acme", string repo = "api", int number = 2071) => _service.Handle(new GetReviewPageRequest(owner, repo, number), Token);

    [Theory]
    [InlineData("..", "api", 1)]
    [InlineData("acme", "a/b", 1)]
    [InlineData("acme", "api", 0)]
    public async Task A_path_segment_that_fails_GitHubs_rules_is_refused_before_anything_is_read(string owner, string repo, int number)
    {
        await Approve();

        Assert.Equal(SindriErrors.InvalidInput.Code, (await Read(owner, repo, number)).Error.Code);
        Assert.Empty(_store.Asked);
        Assert.Empty(_forge.Calls);
    }

    [Fact]
    public async Task Without_a_login_the_page_is_refused_before_the_forge_is_asked()
    {
        await Approve(login: null);

        var error = (await Read()).Error;

        Assert.Equal((SindriErrors.ReviewNoLogin.Code, ErrorKind.Conflict), (error.Code, error.Kind));
        Assert.Empty(_forge.Calls);
    }

    [Fact]
    public async Task A_repo_sindri_does_not_sync_is_refused_before_the_forge_is_asked()
    {
        await Approve();

        var error = (await Read(repo: "other")).Error;

        Assert.Equal(SindriErrors.ReviewNotSynced.Code, error.Code);
        Assert.Equal("Sindri doesn't sync acme/other. Add it to `context.sources.github.repos`.", error.Message);
        Assert.Empty(_forge.Calls);
    }

    [Fact]
    public async Task A_pr_the_forge_does_not_have_is_not_found()
    {
        await Approve();

        Assert.Equal((SindriErrors.ReviewPrNotFound.Code, "PR not found"), ((await Read(number: 9999)).Error.Code, (await Read(number: 9999)).Error.Message));
    }

    [Fact]
    public async Task The_page_is_built_from_the_forge_cached_and_its_summary_stored_for_the_list()
    {
        await Approve();

        var page = (await Read()).Value;

        Assert.Equal((Head, "sam-dev", false, false), (page.Pr.Head.Sha, page.Pr.Author, page.Pr.Yours, page.Pr.PostsFixThreads));
        Assert.Equal("src/A.cs", Assert.Single(page.Diff.Groups.SelectMany(static g => g.Files)).Path);
        var cached = (await new FileReviewPages(new FakeSindriHome(_home.Path), _h.Clock).Newest(new ReviewPullRef("acme", "api", 2071), Token)).Value;
        Assert.Equal(Head, cached?.Page.Pr.Head.Sha);
        var summary = (await _summaries.Read(new ReviewPullRef("acme", "api", 2071), Head, Token)).Value;
        Assert.Equal(ReviewCi.Fail, summary?.Ci);
    }

    [Fact]
    public async Task A_credential_that_posts_as_the_author_makes_the_pr_one_a_fix_thread_can_be_posted_on()
    {
        await Approve();
        _forge.Pulls[0].Author = "sindri-agent";

        var pr = (await Read()).Value.Pr;

        Assert.Equal((false, true), (pr.Yours, pr.PostsFixThreads));
    }

    [Fact]
    public async Task A_head_that_moves_twice_shows_the_newest_reads_with_the_moving_banner()
    {
        await Approve();
        foreach (var head in new[] { new string('1', 40), new string('2', 40), new string('3', 40), Head })
        {
            _forge.HeadScript.Enqueue(head);
        }

        var page = (await Read()).Value;

        Assert.Equal(("The PR is moving; reload when it settles.", Head), (page.Moving?.Text, page.Pr.Head.Sha));
    }

    [Fact]
    public async Task A_head_that_moves_once_is_read_again_and_shows_no_banner()
    {
        await Approve();
        foreach (var head in new[] { new string('1', 40), new string('2', 40), new string('2', 40), new string('2', 40) })
        {
            _forge.HeadScript.Enqueue(head);
        }

        var page = (await Read()).Value;

        Assert.Null(page.Moving);
        Assert.Equal(new string('2', 40), page.Pr.Head.Sha);
    }

    [Fact]
    public async Task When_the_forge_cannot_be_reached_the_newest_cached_page_answers_with_the_time_it_was_cached()
    {
        await Approve();
        _ = (await Read()).Value;
        _forge.GetFailure = SindriErrors.ForgeUnavailable.With("github can't be reached");
        _h.Clock.Advance(TimeSpan.FromMinutes(14));

        var page = (await Read()).Value;

        Assert.Equal(("as of 00:00, the PR may have moved", KnownInstants.Epoch), (page.Offline?.Text, page.Offline?.CachedAt));
        Assert.Equal(Head, page.Pr.Head.Sha);
    }

    [Fact]
    public async Task When_the_forge_cannot_be_reached_and_nothing_is_cached_the_answer_is_503()
    {
        await Approve();
        _forge.GetFailure = SindriErrors.ForgeUnavailable.With("github can't be reached");

        var error = (await Read()).Error;

        Assert.Equal((SindriErrors.ReviewOfflineUncached.Code, ErrorKind.Unavailable, "Offline, and this PR isn't cached yet"), (error.Code, error.Kind, error.Message));
    }

    [Fact]
    public async Task A_forge_that_refuses_the_credential_still_serves_the_cache_and_names_the_refusal()
    {
        await Approve();
        _ = (await Read()).Value;
        _forge.GetFailure = SindriErrors.ForgeRefused.With("bad credentials");

        var page = (await Read()).Value;

        Assert.Equal(Head, page.Pr.Head.Sha);
        Assert.Contains("GitHub refused: SND-FORGE-002", page.Offline?.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_forge_that_refuses_the_credential_with_nothing_cached_names_the_refusal_in_the_503()
    {
        await Approve();
        _forge.GetFailure = SindriErrors.ForgeRefused.With("bad credentials");

        var error = (await Read()).Error;

        Assert.Equal((SindriErrors.ReviewOfflineUncached.Code, ErrorKind.Unavailable), (error.Code, error.Kind));
        Assert.Contains("GitHub refused: SND-FORGE-002", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failed_files_read_makes_error_panels_not_a_failed_page()
    {
        await Approve();
        _forge.FilesFailure = SindriErrors.ForgeUnavailable.With("github returned 502");

        var page = (await Read()).Value;

        Assert.Equal(("error", "Couldn't read the diff: github returned 502"), (page.Diff.State, page.Diff.Text));
        Assert.Null(page.Offline);
    }

    private sealed class Status : IProjectorStatus
    {
        public ViewsStatus Current { get; } = new(1, 1, ProjectedAt: null, Rebuilding: false, Stalled: false, StalledAt: null, StalledType: null);

        public ValueTask<Result<Unit>> WaitFor(long sequence, TimeSpan limit, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success(Unit.Value));
    }

    [Fact]
    public async Task An_invalid_live_profile_reads_the_page_like_no_profile()
    {
        var none = await Read();
        _h.Profiles.Live = SindriErrors.ProfileInvalid.With("/home/thor/profile.yaml: line 3: unknown key 'tennant'");

        var invalid = await Read();

        Assert.Equal(none.IsError ? none.Error.Code : "ok", invalid.IsError ? invalid.Error.Code : "ok");
    }
}
