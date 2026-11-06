using Aw.Kernel.Testing;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Features.ListReviews;
using Aw.Sindri.Infrastructure.Reviews;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.Time.Testing;
using static Aw.Sindri.Tests.Reviews.ReviewPulls;

namespace Aw.Sindri.Tests.Reviews;

/// <summary>The per-head summary behind a row's CI and required checks (PR review page spec §6.1, §6.2 "Cache"): stored first, built from the forge for a head with none, kept when the forge fails.</summary>
[Lane(TestLane.Integration)]
public sealed class ReviewSummaryReaderTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly FakePullRequests _forge = new(["github.com"]);
    private readonly FileReviewSummaries _cache;
    private readonly ReviewSummaryReader _reader;

    public ReviewSummaryReaderTests()
    {
        _cache = new FileReviewSummaries(new FakeSindriHome(_home.Path));
        _reader = new ReviewSummaryReader(_cache, _forge, new FakeTimeProvider(KnownInstants.Epoch));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static PrReviewSettings Strict() => new(
        Settings: new Dictionary<string, PrReviewSettingsEntry>(StringComparer.Ordinal)
        {
            ["strict"] = new(Checks: [new PrReviewCheck("ui", "UI tests ran", Need: new PrReviewNeed(Ci: "ui-tests")), new PrReviewCheck("ran", "I ran it", Need: new PrReviewNeed(Manual: true))]),
        },
        Projects: [new PrReviewProject("prj_1", ["acme/api"], Settings: "strict")]);

    public void Dispose() => _home.Dispose();

    private FakePull Seed(int number, string sha, params PullCheckRun[] runs)
    {
        var pull = _forge.Add(new PullRepo("github.com", "acme", "api"), "feat/x", sha);
        pull.Checks = new PullChecks(PullChecksState.Pending, sha, [], Passed: 0, Pending: 0, runs);
        Assert.Equal(number, pull.Number);
        return pull;
    }

    [Fact]
    public async Task A_head_with_a_stored_summary_costs_no_forge_call()
    {
        var stored = new ReviewSummary(Head, ReviewCi.Pass, 0, KnownInstants.Epoch);
        _ = await _cache.Write(new ReviewPullRef("acme", "api", 1), stored, Token);

        var read = await _reader.Read([Pull(number: 1, url: "https://github.com/acme/api/pull/1")], Strict(), Token);

        Assert.Equal(stored, read[("acme/api", 1)]);
        Assert.Empty(_forge.Calls);
    }

    [Fact]
    public async Task A_head_with_none_is_read_decided_and_stored()
    {
        var pull = Seed(1, Head, new PullCheckRun("ui-tests", "github-actions", "success", Head));
        pull.Files = [new PullFile("src/A.cs", PreviousPath: null, PullFileStatus.Modified, 1, 0, Patch: null)];

        var read = await _reader.Read([Pull(number: 1, url: pull.Link)], Strict(), Token);

        var summary = read[("acme/api", 1)];
        Assert.Equal((Head, ReviewCi.Pass, 1), (summary.Sha, summary.Ci, summary.RequiredOpen));   // ui passed, the manual check needs a tick
        var stored = await _cache.Read(new ReviewPullRef("acme", "api", 1), Head, Token);
        Assert.Equal(summary, stored.Value);
    }

    [Fact]
    public async Task A_PR_whose_settings_name_no_checks_has_no_required_count()
    {
        var pull = Seed(1, Head, new PullCheckRun("ui-tests", "github-actions", "failure", Head));

        var read = await _reader.Read([Pull(number: 1, url: pull.Link)], review: null, Token);

        Assert.Equal((ReviewCi.Fail, null), (read[("acme/api", 1)].Ci, read[("acme/api", 1)].RequiredOpen));
    }

    [Fact]
    public async Task A_forge_failure_keeps_the_newest_summary_of_an_older_head()   // the PR is not on the fake forge, so Get answers an error
    {
        var older = new ReviewSummary(Old, ReviewCi.Running, 3, KnownInstants.Epoch.AddDays(-1));
        _ = await _cache.Write(new ReviewPullRef("acme", "api", 1), older, Token);

        var read = await _reader.Read([Pull(number: 1, url: "https://github.com/acme/api/pull/1")], Strict(), Token);

        Assert.Equal(older, read[("acme/api", 1)]);
    }

    [Fact]
    public async Task A_forge_failure_with_nothing_stored_leaves_the_row_without_a_summary()   // not on the fake forge: Get answers an error
    {

        var read = await _reader.Read([Pull(number: 1, url: "https://github.com/acme/api/pull/1")], Strict(), Token);

        Assert.Empty(read);
    }

    [Fact]
    public async Task A_failure_stops_reading_new_heads_for_the_call()   // none of the 20 is on the fake forge
    {
        var pulls = Enumerable.Range(1, 20).Select(n => Pull(number: n, url: string.Create(System.Globalization.CultureInfo.InvariantCulture, $"https://github.com/acme/api/pull/{n}"))).ToList();

        var read = await _reader.Read(pulls, Strict(), Token);

        Assert.Empty(read);
        Assert.InRange(_forge.Calls.Count(static c => c == "Get"), 1, ReviewSummaryReader.MaxHeadsAtOnce);
    }

    [Fact]
    public async Task A_PR_with_no_head_or_link_uses_only_what_is_stored()
    {
        var read = await _reader.Read([Pull(number: 1, head: null), Pull(number: 2, url: null)], Strict(), Token);

        Assert.Empty(read);
        Assert.Empty(_forge.Calls);
    }
}
