using Aw.Kernel.Testing;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Tests.Reviews;

/// <summary>The required-check evaluator (PR review page spec §7.3): which checks apply, and how each need stands on the head.</summary>
[Lane(TestLane.Unit)]
public sealed class RequiredChecksTests
{
    private const string Head = ReviewPulls.Head;
    private const string Old = ReviewPulls.Old;

    private static PullCheckRun Run(string name, string conclusion, string sha = Head, string? app = null) => new(name, app, conclusion, sha);

    private static PrReviewCheck Check(string id, PrReviewNeed need, params string[] when) => new(id, "Title " + id, when.Length == 0 ? null : when, need);

    private static RequiredCheckResult One(PrReviewCheck check, PullCheckRun[]? runs = null, string[]? paths = null, string? body = null, string[]? ticked = null, string[]? covers = null, Func<string, bool>? resolves = null) =>
        Assert.Single(RequiredChecks.Evaluate(new RequiredCheckInputs([check], paths ?? [], Head, runs ?? [], body, new HashSet<string>(ticked ?? [], StringComparer.Ordinal), covers, resolves ?? (static _ => true))));

    [Fact]
    public void A_ci_check_passes_when_the_named_run_succeeded_on_the_head()
    {
        var result = One(Check("ui", new PrReviewNeed(Ci: "ui-tests")), [Run("ui-tests", "success")]);

        Assert.Equal((RequiredCheckStatus.Pass, "ci"), (result.Status, result.Need));
        Assert.False(result.IsOpen);
    }

    [Fact]
    public void A_ci_check_fails_when_the_run_failed_and_says_which_commit()
    {
        var result = One(Check("ui", new PrReviewNeed(Ci: "ui-tests")), [Run("ui-tests", "failure")]);

        Assert.Equal(RequiredCheckStatus.Fail, result.Status);
        Assert.Equal("ui-tests: failure on aaaaaaa", result.Detail);
    }

    [Theory]
    [InlineData("skipped")]
    [InlineData("neutral")]
    public void A_skipped_or_neutral_run_shows_skipped_and_does_not_pass(string conclusion)
    {
        var result = One(Check("ui", new PrReviewNeed(Ci: "ui-tests")), [Run("ui-tests", conclusion)]);

        Assert.Equal(RequiredCheckStatus.Skipped, result.Status);
        Assert.True(result.IsOpen);
    }

    [Fact]
    public void A_run_on_an_older_commit_is_running_until_the_heads_run_arrives()
    {
        Assert.Equal(RequiredCheckStatus.Running, One(Check("ui", new PrReviewNeed(Ci: "ui-tests")), [Run("ui-tests", "success", Old)]).Status);
        Assert.Equal(RequiredCheckStatus.Running, One(Check("ui", new PrReviewNeed(Ci: "ui-tests"))).Status);
    }

    [Fact]
    public void A_run_with_no_conclusion_yet_is_running() =>
        Assert.Equal(RequiredCheckStatus.Running, One(Check("ui", new PrReviewNeed(Ci: "ui-tests")), [Run("ui-tests", "pending")]).Status);

    [Fact]
    public void The_run_name_is_case_sensitive_and_a_named_app_must_match()
    {
        Assert.Equal(RequiredCheckStatus.Running, One(Check("ui", new PrReviewNeed(Ci: "ui-tests")), [Run("UI-Tests", "success")]).Status);
        Assert.Equal(RequiredCheckStatus.Running, One(Check("ui", new PrReviewNeed(Ci: "ui-tests", App: "github-actions")), [Run("ui-tests", "success", app: "circleci")]).Status);
        Assert.Equal(RequiredCheckStatus.Pass, One(Check("ui", new PrReviewNeed(Ci: "ui-tests", App: "github-actions")), [Run("ui-tests", "success", app: "github-actions")]).Status);
    }

    [Fact]
    public void A_changedWith_check_passes_only_when_every_glob_has_a_changed_file()
    {
        var check = Check("locks", new PrReviewNeed(ChangedWith: ["**/packages.lock.json", "**/*.csproj"]));

        Assert.Equal(RequiredCheckStatus.Pass, One(check, paths: ["src/A/A.csproj", "src/A/packages.lock.json"]).Status);
        Assert.Equal(RequiredCheckStatus.Fail, One(check, paths: ["src/A/A.csproj"]).Status);
    }

    [Fact]
    public void A_covers_check_needs_a_Covers_line_and_every_ref_resolving()
    {
        var check = Check("covers", new PrReviewNeed(Covers: true));

        Assert.Equal(RequiredCheckStatus.Pass, One(check, covers: ["doc#Ph1", "doc#Ph2"]).Status);
        Assert.Equal(RequiredCheckStatus.Fail, One(check, covers: null).Status);
        Assert.Equal(RequiredCheckStatus.Fail, One(check, covers: []).Status);
        Assert.Equal(RequiredCheckStatus.Fail, One(check, covers: ["doc#Ph1", "doc#Ph9"], resolves: r => r != "doc#Ph9").Status);
    }

    [Fact]
    public void A_body_line_match_is_claimed_and_never_a_pass()
    {
        var check = Check("ideas", new PrReviewNeed(Body: "^reviewed by ideas at [0-9a-f]{7,40}$"));

        var result = One(check, body: "Summary\r\nreviewed by ideas at abc1234\r\nmore");

        Assert.Equal(RequiredCheckStatus.Claimed, result.Status);
        Assert.True(result.IsOpen);
        Assert.Equal(RequiredCheckStatus.Fail, One(check, body: "reviewed by ideas at abc1234 and then some").Status);
        Assert.Equal(RequiredCheckStatus.Fail, One(check, body: null).Status);
    }

    [Fact]
    public void A_regex_that_does_not_compile_is_a_fail_not_an_exception() =>
        Assert.Equal(RequiredCheckStatus.Fail, One(Check("bad", new PrReviewNeed(Body: "([")), body: "text").Status);

    [Fact]
    public void A_manual_check_needs_a_tick_until_it_is_ticked_on_this_head()
    {
        var check = Check("ran-it", new PrReviewNeed(Manual: true));

        Assert.Equal(RequiredCheckStatus.NeedsTick, One(check).Status);
        var ticked = One(check, ticked: ["ran-it"]);
        Assert.Equal(RequiredCheckStatus.Ticked, ticked.Status);
        Assert.False(ticked.IsOpen);
    }

    private static IReadOnlyList<RequiredCheckResult> Applying(PrReviewCheck check, params string[] paths) =>
        RequiredChecks.Evaluate(new RequiredCheckInputs([check], paths, Head, [], Body: null, new HashSet<string>(StringComparer.Ordinal), CoversRefs: null, static _ => true));

    [Fact]
    public void A_check_applies_when_any_changed_file_matches_any_of_its_when_globs_and_always_with_none()
    {
        var gated = Check("env", new PrReviewNeed(Manual: true), "src/**/*.cs", "scripts/**/*.sh");
        var always = Check("ran-it", new PrReviewNeed(Manual: true));

        Assert.Empty(Applying(gated, "docs/a.md"));
        _ = Assert.Single(Applying(gated, "scripts/x/run.sh"));
        _ = Assert.Single(Applying(always));
    }

    [Theory]
    [InlineData(RequiredCheckStatus.Pass, false)]
    [InlineData(RequiredCheckStatus.Ticked, false)]
    [InlineData(RequiredCheckStatus.Fail, true)]
    [InlineData(RequiredCheckStatus.Running, true)]
    [InlineData(RequiredCheckStatus.Skipped, true)]
    [InlineData(RequiredCheckStatus.Claimed, true)]
    [InlineData(RequiredCheckStatus.NeedsTick, true)]
    public void Open_is_everything_that_is_not_a_pass_or_a_tick(RequiredCheckStatus status, bool open)
    {
        var result = new RequiredCheckResult("id", "Title", "ci", status, Detail: null);

        Assert.Equal(open, result.IsOpen);
        Assert.Equal(open ? 1 : 0, RequiredChecks.OpenCount([result]));
    }

    [Fact]
    public void The_ci_roll_up_is_fail_over_running_over_pass_and_none_without_a_run_on_the_head()
    {
        Assert.Equal(ReviewCi.Fail, RequiredChecks.RollUp([Run("a", "success"), Run("b", "failure"), Run("c", "pending")], Head));
        Assert.Equal(ReviewCi.Running, RequiredChecks.RollUp([Run("a", "success"), Run("c", "pending")], Head));
        Assert.Equal(ReviewCi.Pass, RequiredChecks.RollUp([Run("a", "success"), Run("b", "skipped")], Head));
        Assert.Equal(ReviewCi.None, RequiredChecks.RollUp([], Head));
        Assert.Equal(ReviewCi.None, RequiredChecks.RollUp([Run("a", "failure", Old)], Head));
    }
}
