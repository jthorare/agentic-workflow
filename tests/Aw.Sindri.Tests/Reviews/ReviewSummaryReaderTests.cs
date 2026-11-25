using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Features.ListReviews;
using Aw.Sindri.Infrastructure.Reviews;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Server;
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
    private readonly ListLogger<ReviewSummaryReader> _log = new();
    private readonly ReviewSummaryReader _reader;

    public ReviewSummaryReaderTests()
    {
        _cache = new FileReviewSummaries(new FakeSindriHome(_home.Path));
        _reader = new ReviewSummaryReader(_cache, _forge, new FakeTimeProvider(KnownInstants.Epoch), _log);
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
    public async Task A_stored_running_summary_is_rebuilt_from_the_forge()
    {
        var pull = Seed(1, Head, new PullCheckRun("ui-tests", "github-actions", "success", Head));
        _ = await _cache.Write(new ReviewPullRef("acme", "api", 1), new ReviewSummary(Head, ReviewCi.Running, 1, KnownInstants.Epoch.AddMinutes(-1)), Token);

        var read = await _reader.Read([Pull(number: 1, url: pull.Link)], Strict(), Token);

        Assert.Equal(ReviewCi.Pass, read[("acme/api", 1)].Ci);
    }

    [Fact]
    public async Task A_PR_the_forge_does_not_have_skips_only_itself()
    {
        var pull = Seed(1, Head, new PullCheckRun("ui-tests", "github-actions", "success", Head));
        List<ReviewPull> pulls = [Pull(number: 7, url: "https://github.com/acme/api/pull/7"), Pull(number: 1, url: pull.Link)];

        var read = await _reader.Read(pulls, Strict(), Token);

        Assert.Equal([("acme/api", 1)], read.Keys);
    }

    [Fact]
    public async Task A_summary_stored_under_the_forges_head_is_found_without_reading_the_files()
    {
        var pull = Seed(1, Old, new PullCheckRun("ui-tests", "github-actions", "success", Old));
        var stored = new ReviewSummary(Old, ReviewCi.Pass, 0, KnownInstants.Epoch);
        _ = await _cache.Write(new ReviewPullRef("acme", "api", 1), stored, Token);

        var read = await _reader.Read([Pull(number: 1, head: Head, url: pull.Link)], Strict(), Token);

        Assert.Equal(stored, read[("acme/api", 1)]);
        Assert.DoesNotContain(_forge.Calls, static c => string.Equals(c, "Files", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_open_count_leaves_out_body_and_manual_checks()
    {
        var settings = new PrReviewSettings(
            Settings: new Dictionary<string, PrReviewSettingsEntry>(StringComparer.Ordinal)
            {
                ["s"] = new(Checks: [new PrReviewCheck("ui", "UI", Need: new PrReviewNeed(Ci: "ui-tests")), new PrReviewCheck("b", "Body", Need: new PrReviewNeed(Body: "tested")), new PrReviewCheck("m", "Manual", Need: new PrReviewNeed(Manual: true))]),
            },
            Projects: [new PrReviewProject("prj_1", ["acme/api"], Settings: "s")]);
        var pull = Seed(1, Head, new PullCheckRun("ui-tests", "github-actions", "failure", Head));

        var read = await _reader.Read([Pull(number: 1, url: pull.Link)], settings, Token);

        Assert.Equal(1, read[("acme/api", 1)].RequiredOpen);
    }

    [Fact]
    public async Task A_head_with_none_is_read_decided_and_stored()
    {
        var pull = Seed(1, Head, new PullCheckRun("ui-tests", "github-actions", "success", Head));
        pull.Files = [new PullFile("src/A.cs", PreviousPath: null, PullFileStatus.Modified, 1, 0, Patch: null)];

        var read = await _reader.Read([Pull(number: 1, url: pull.Link)], Strict(), Token);

        var summary = read[("acme/api", 1)];
        Assert.Equal((Head, ReviewCi.Pass, 0), (summary.Sha, summary.Ci, summary.RequiredOpen));   // ui passed; the manual check is the page's, not counted
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
    public async Task A_refusal_stops_reading_new_heads_for_the_call()   // the host is not on the profile's list, so Get answers a refusal
    {
        var pulls = Enumerable.Range(1, 20).Select(n => Pull(number: n, url: string.Create(System.Globalization.CultureInfo.InvariantCulture, $"https://other.example/acme/api/pull/{n}"))).ToList();

        var read = await _reader.Read(pulls, Strict(), Token);

        Assert.Empty(read);
        Assert.InRange(_forge.Calls.Count(static c => c == "Get"), 1, ReviewSummaryReader.MaxHeadsAtOnce);
    }

    [Fact]
    public async Task A_stored_running_summary_is_kept_once_the_pass_has_stopped()   // the first 20 are refused by the host check; the 21st has a running summary stored
    {
        var running = new ReviewSummary(Head, ReviewCi.Running, 1, KnownInstants.Epoch.AddMinutes(-1));
        _ = await _cache.Write(new ReviewPullRef("acme", "api", 21), running, Token);
        var pulls = Enumerable.Range(1, 21).Select(n => Pull(number: n, url: string.Create(System.Globalization.CultureInfo.InvariantCulture, $"https://other.example/acme/api/pull/{n}"))).ToList();

        var read = await _reader.Read(pulls, Strict(), Token);

        Assert.Equal(running, read[("acme/api", 21)]);
        Assert.InRange(_forge.Calls.Count(static c => c == "Get"), 1, ReviewSummaryReader.MaxHeadsAtOnce);
    }

    [Fact]
    public async Task A_PR_with_no_head_or_link_uses_only_what_is_stored()
    {
        var read = await _reader.Read([Pull(number: 1, head: null), Pull(number: 2, url: null)], Strict(), Token);

        Assert.Empty(read);
        Assert.Empty(_forge.Calls);
    }

    private static Error RepoRefused() => SindriErrors.ForgeRepoRefused.With("AW-FORGE-009: The forge refused this request for the repo: Resource not accessible");

    [Fact]
    public async Task A_repo_the_forge_refuses_skips_its_PRs_and_not_the_other_repos()
    {
        _forge.GetFailuresByRepo["acme/locked"] = RepoRefused();
        var open = _forge.Add(new PullRepo("github.com", "acme", "api"), "feat/x", Head);
        open.Checks = new PullChecks(PullChecksState.Passing, Head, [], Passed: 1, Pending: 0, [new PullCheckRun("ui-tests", "github-actions", "success", Head)]);
        List<ReviewPull> pulls = [.. Enumerable.Range(1, 6).Select(n => Pull(repo: "acme/locked", number: n, url: string.Create(System.Globalization.CultureInfo.InvariantCulture, $"https://github.com/acme/locked/pull/{n}"))), Pull(repo: "acme/api", number: 1, url: open.Link)];

        var read = await _reader.Read(pulls, Strict(), Token);

        Assert.Equal([("acme/api", 1)], read.Keys);
        Assert.Equal(ReviewCi.Pass, read[("acme/api", 1)].Ci);
    }

    [Fact]
    public async Task A_refused_repo_is_read_once_per_call()
    {
        _forge.GetFailuresByRepo["acme/locked"] = RepoRefused();
        var pulls = Enumerable.Range(1, 12).Select(n => Pull(repo: "acme/locked", number: n, url: string.Create(System.Globalization.CultureInfo.InvariantCulture, $"https://github.com/acme/locked/pull/{n}"))).ToList();

        _ = await _reader.Read(pulls, Strict(), Token);

        Assert.InRange(_forge.Calls.Count(static c => c == "Get"), 1, ReviewSummaryReader.MaxHeadsAtOnce);   // only the first batch in flight reads; the rest see the repo is refused
    }

    [Fact]
    public async Task A_refused_repo_warns_once_per_call_with_SND_FORGE_005()
    {
        _forge.GetFailuresByRepo["acme/locked"] = RepoRefused();
        var pulls = Enumerable.Range(1, 12).Select(n => Pull(repo: "acme/locked", number: n, url: string.Create(System.Globalization.CultureInfo.InvariantCulture, $"https://github.com/acme/locked/pull/{n}"))).ToList();

        _ = await _reader.Read(pulls, Strict(), Token);

        var line = Assert.Single(_log.Lines);
        Assert.StartsWith("SND-FORGE-005 the forge refused acme/locked", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_credential_error_still_stops_the_call()
    {
        _forge.GetFailure = SindriErrors.ForgeRefused.With("AW-FORGE-003: The forge credential is missing or was rejected");
        var open = _forge.Add(new PullRepo("github.com", "acme", "api"), "feat/x", Head);
        var pulls = Enumerable.Range(1, 12).Select(n => Pull(repo: "acme/other", number: n, url: string.Create(System.Globalization.CultureInfo.InvariantCulture, $"https://github.com/acme/other/pull/{n}"))).ToList();

        var read = await _reader.Read([.. pulls, Pull(repo: "acme/api", number: 1, url: open.Link)], Strict(), Token);

        Assert.Empty(read);
        Assert.InRange(_forge.Calls.Count(static c => c == "Get"), 1, ReviewSummaryReader.MaxHeadsAtOnce);
    }
}
