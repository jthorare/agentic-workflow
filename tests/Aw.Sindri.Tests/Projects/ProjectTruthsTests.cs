using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Projects.Truth;
using Aw.Sindri.Core.Projects.TruthSources;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Tests.Projects;

/// <summary>A project's truth from Sindri's own data (issue 3718): sources from links, PR states, items, agents and the derived health. Pure; no Linear anywhere.</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectTruthsTests
{
    private const string Api = "acme/api";
    private const string Web = "acme/web";
    private static readonly DateTimeOffset _now = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly ProjectSources _apiOnly = new([Api], []);
    private static readonly string[] _webPull = ["https://github.com/acme/web/pull/7"];
    private static readonly bool[] _firstWaitsOnYou = [true, false];
    private static readonly string[] _apiAndInfra = ["acme/api", "acme/infra"];
    private static readonly int[] _seven = [7];
    private static readonly ProjectPrState[] _precedence = [ProjectPrState.Draft, ProjectPrState.CiRed, ProjectPrState.Unknown, ProjectPrState.Ready, ProjectPrState.Draft];

    private static ReviewPull Pull(int number, string repo = Api, bool draft = false, string[]? requested = null, string sha = "abc", ReviewedAt[]? reviews = null) =>
        new(repo, number, $"PR {number.ToString(CultureInfo.InvariantCulture)}", "sam", draft, _now.AddDays(-5), _now.AddDays(-1), sha, requested ?? [], reviews ?? [], ReviewersRead: true, [], $"https://github.com/{repo}/pull/{number.ToString(CultureInfo.InvariantCulture)}");

    // #4333: a PR the forge says cannot merge (Mergeable false) or that has a reviewer's latest review at CHANGES_REQUESTED; null is unknown and counts for neither
    private static ReviewPull Conflicted(int number) => Pull(number) with { Mergeable = false };

    private static ReviewPull Changes(int number) => Pull(number) with { Reviews = [new ReviewedAt("sam", "abc", NewCommits: 0, State: "CHANGES_REQUESTED")] };

    private static string Key(int number, string repo = Api) => $"{repo}#{number.ToString(CultureInfo.InvariantCulture)}";

    // every pull gets a passing summary of its head unless 'summaries' overrides it or 'unchecked' leaves it out
    private static ProjectTruthInputs Inputs(
        ProjectSources? sources = null,
        IReadOnlyList<ReviewPull>? pulls = null,
        IReadOnlyDictionary<string, ReviewSummary>? summaries = null,
        IReadOnlyList<ProjectTruthItem>? items = null,
        IReadOnlyList<ProjectTruthAgent>? agents = null,
        bool noSummaries = false,
        string? owner = null,
        DateTimeOffset? now = null)
    {
        var all = new Dictionary<string, ReviewSummary>(StringComparer.Ordinal);
        foreach (var pull in pulls ?? [])
        {
            if (!noSummaries)
            {
                all[Key(pull.Number, pull.Repo)] = new ReviewSummary(pull.HeadSha!, ReviewCi.Pass, RequiredOpen: null, _now.AddDays(-1));
            }
        }

        foreach (var (key, summary) in summaries ?? new Dictionary<string, ReviewSummary>(StringComparer.Ordinal))
        {
            all[key] = summary;
        }

        return new(sources ?? _apiOnly, new ProjectTruthFacts(pulls ?? [], all, items ?? [], agents ?? [], owner), now);
    }

    private static ReviewSummary Red(DateTimeOffset at) => new("abc", ReviewCi.Fail, RequiredOpen: null, at);

    [Fact]
    public void Sources_come_from_links_that_name_a_github_pr_or_repo_and_from_the_linear_and_profile_repos()
    {
        var sources = ProjectTruths.SourcesOf(
            ["https://github.com/acme/web/pull/7", "https://github.com/acme/api", "https://example.com/plan"],
            linearRepos: ["acme/infra"],
            profileRepos: ["acme/api"]);

        Assert.Equal(_webPull, sources.PullUrls.ToArray());
        Assert.Equal(_apiAndInfra, sources.Repos.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void Sources_list_a_repo_once_when_a_link_and_the_profile_both_name_it()
    {
        var sources = ProjectTruths.SourcesOf(["https://github.com/acme/api"], linearRepos: [], profileRepos: [Api]);

        Assert.Equal(new[] { Api }, sources.Repos.ToArray());
    }

    [Fact]
    public void A_pr_in_a_source_repo_is_open_and_ready_when_nothing_blocks_it()
    {
        var truth = ProjectTruths.Build(Inputs(pulls: [Pull(1), Pull(2, Web)]));

        var pr = Assert.Single(truth.OpenPrs);
        Assert.Equal((1, ProjectPrState.Ready, "https://github.com/acme/api/pull/1"), (pr.Number, pr.State, pr.Url));
    }

    [Fact]
    public void A_pr_named_by_a_link_counts_although_its_repo_is_not_a_source()
    {
        var sources = new ProjectSources([], ["https://github.com/acme/web/pull/7"]);

        var truth = ProjectTruths.Build(Inputs(sources, pulls: [Pull(7, Web), Pull(8, Web)]));

        Assert.Equal(_seven, truth.OpenPrs.Select(static p => p.Number).ToArray());
    }

    [Fact]
    public void A_draft_pr_is_draft()
    {
        var truth = ProjectTruths.Build(Inputs(pulls: [Pull(1, draft: true)]));

        Assert.Equal(ProjectPrState.Draft, truth.OpenPrs.Single().State);
    }

    [Fact]
    public void A_pr_whose_head_failed_ci_is_ci_red_since_the_summary_was_decided()
    {
        var at = _now.AddDays(-2);

        var truth = ProjectTruths.Build(Inputs(pulls: [Pull(1)], summaries: new Dictionary<string, ReviewSummary>(StringComparer.Ordinal) { [Key(1)] = Red(at) }));

        var pr = truth.OpenPrs.Single();
        Assert.Equal((ProjectPrState.CiRed, (DateTimeOffset?)at), (pr.State, pr.DecidedAt));
    }

    [Fact]
    public void A_summary_of_an_older_head_is_not_a_check_of_this_head_so_the_pr_is_unknown()
    {
        var stale = new ReviewSummary("old", ReviewCi.Fail, RequiredOpen: null, _now.AddDays(-9));

        var truth = ProjectTruths.Build(Inputs(pulls: [Pull(1, sha: "new")], summaries: new Dictionary<string, ReviewSummary>(StringComparer.Ordinal) { [Key(1)] = stale }));

        Assert.Equal(ProjectPrState.Unknown, truth.OpenPrs.Single().State);
    }

    [Fact]
    public void A_pr_with_no_summary_at_all_is_unknown_never_ready_and_has_no_decided_at()
    {
        var truth = ProjectTruths.Build(Inputs(pulls: [Pull(1)], noSummaries: true));

        var pr = truth.OpenPrs.Single();
        Assert.Equal(ProjectPrState.Unknown, pr.State);
        Assert.Null(pr.DecidedAt);
    }

    [Fact]
    public void A_pr_with_a_requested_review_waits_on_those_reviewers()
    {
        var truth = ProjectTruths.Build(Inputs(pulls: [Pull(1, requested: ["joi", "sam"])]));

        var pr = truth.OpenPrs.Single();
        Assert.Equal((ProjectPrState.Waiting, "joi, sam"), (pr.State, pr.WaitingOn));
    }

    [Fact]
    public void A_pr_with_commits_after_a_review_waits_on_that_reviewer()
    {
        var truth = ProjectTruths.Build(Inputs(pulls: [Pull(1, reviews: [new ReviewedAt("joi", "abc", NewCommits: 2)])]));

        var pr = truth.OpenPrs.Single();
        Assert.Equal((ProjectPrState.Waiting, "joi"), (pr.State, pr.WaitingOn));
    }

    [Fact]
    public void A_pr_that_waits_on_a_reviewer_with_no_ci_result_on_its_head_is_not_checked_so_the_project_is_not_on_track()
    {
        var truth = ProjectTruths.Build(Inputs(pulls: [Pull(1, requested: ["joi"])], noSummaries: true));

        Assert.Equal(ProjectPrState.Waiting, truth.OpenPrs.Single().State);
        Assert.Equal((ProjectTruthLevel.Unknown, "CI not checked yet for 1 of 1 PR"), (truth.Health!.Level, truth.Health.Reason));
    }

    [Fact]
    public void Ci_red_wins_over_waiting()
    {
        var truth = ProjectTruths.Build(Inputs(pulls: [Pull(1, requested: ["joi"])], summaries: new Dictionary<string, ReviewSummary>(StringComparer.Ordinal) { [Key(1)] = Red(_now) }));

        Assert.Equal(ProjectPrState.CiRed, truth.OpenPrs.Single().State);
    }

    [Fact]
    public void Items_count_when_a_link_names_one_of_the_projects_prs_and_split_into_open_and_done()
    {
        var link = "https://github.com/acme/api/pull/1";
        var items = new[] { new ProjectTruthItem(Api, Done: false, [link]), new ProjectTruthItem(Api, Done: true, [link]), new ProjectTruthItem(Api, Done: false, ["https://github.com/acme/api/pull/99"]) };

        var truth = ProjectTruths.Build(Inputs(pulls: [Pull(1)], items: items));

        Assert.Equal((1, 1, 2), (truth.OpenItems, truth.DoneItems, truth.TotalItems));
    }

    [Fact]
    public void Agents_count_when_running_in_a_source_repo()
    {
        var agents = new[] { new ProjectTruthAgent(Api, Running: true), new ProjectTruthAgent(Api, Running: false), new ProjectTruthAgent(Web, Running: true), new ProjectTruthAgent(Repo: null, Running: true) };

        var truth = ProjectTruths.Build(Inputs(agents: agents));

        Assert.Equal(1, truth.RunningAgents);
    }

    [Fact]
    public void A_project_with_no_prs_and_no_items_has_no_health() => Assert.Null(ProjectTruths.Build(Inputs()).Health);

    [Fact]
    public void Health_is_on_track_when_no_pr_is_red_or_stuck()
    {
        var truth = ProjectTruths.Build(Inputs(pulls: [Pull(1), Pull(2)]));

        Assert.Equal(ProjectTruthLevel.OnTrack, truth.Health!.Level);
    }

    [Fact]
    public void Health_is_at_risk_and_says_how_many_prs_are_red_out_of_how_many()
    {
        ReviewPull[] pulls = [.. Enumerable.Range(1, 9).Select(static n => Pull(n))];
        var summaries = Enumerable.Range(1, 3).ToDictionary(static n => Key(n), _ => Red(_now.AddDays(-2)), StringComparer.Ordinal);

        var truth = ProjectTruths.Build(Inputs(pulls: pulls, summaries: summaries));

        Assert.Equal((ProjectTruthLevel.AtRisk, "At risk: 3 of 9 PRs red"), (truth.Health!.Level, truth.Health.Reason));
    }

    // #4333: the reason says how long the oldest red PR has been red, from the CI summary's own time and the page's clock
    [Theory]
    [InlineData(259_200, "At risk: 3 of 9 PRs red for 3 d")]
    [InlineData(18_000, "At risk: 3 of 9 PRs red for 5 h")]
    [InlineData(900, "At risk: 3 of 9 PRs red for 15 min")]
    [InlineData(20, "At risk: 3 of 9 PRs red for under 1 min")]
    public void Health_names_how_long_the_oldest_red_pr_has_been_red(int oldestSeconds, string reason)
    {
        ReviewPull[] pulls = [.. Enumerable.Range(1, 9).Select(static n => Pull(n))];
        var oldest = _now.AddSeconds(-oldestSeconds);
        var summaries = new Dictionary<string, ReviewSummary>(StringComparer.Ordinal) { [Key(1)] = Red(_now), [Key(2)] = Red(oldest), [Key(3)] = Red(_now.AddSeconds(-oldestSeconds / 2)) };

        var truth = ProjectTruths.Build(Inputs(pulls: pulls, summaries: summaries, now: _now));

        Assert.Equal((ProjectTruthLevel.AtRisk, reason), (truth.Health!.Level, truth.Health.Reason));
    }

    [Fact]
    public void Health_age_comes_before_the_unchecked_tail_and_also_reads_on_an_off_track_project()
    {
        ReviewPull[] pulls = [.. Enumerable.Range(1, 3).Select(static n => Pull(n))];
        var summaries = new Dictionary<string, ReviewSummary>(StringComparer.Ordinal) { [Key(1)] = Red(_now.AddDays(-2)), [Key(2)] = Red(_now.AddDays(-4)) };
        var open = Inputs(pulls: pulls, summaries: summaries, now: _now);
        var facts = open.Facts with { Summaries = open.Facts.Summaries.Where(static e => e.Key != Key(3)).ToDictionary(static e => e.Key, static e => e.Value, StringComparer.Ordinal) };

        var truth = ProjectTruths.Build(open with { Facts = facts });

        Assert.Equal((ProjectTruthLevel.OffTrack, "Off track: 2 of 3 PRs red for 4 d, 1 not checked yet"), (truth.Health!.Level, truth.Health.Reason));
    }

    [Fact]
    public void Health_age_is_left_out_when_the_page_gave_no_clock_or_no_red_pr_has_a_time()
    {
        ReviewPull[] pulls = [.. Enumerable.Range(1, 9).Select(static n => Pull(n))];
        var summaries = Enumerable.Range(1, 3).ToDictionary(static n => Key(n), _ => Red(_now.AddDays(-2)), StringComparer.Ordinal);

        var without = ProjectTruths.Build(Inputs(pulls: pulls, summaries: summaries));

        Assert.Equal("At risk: 3 of 9 PRs red", without.Health!.Reason);
    }

    [Fact]
    public void Health_says_how_many_prs_have_conflicts_and_changes_requested_after_the_red_part_and_before_the_unchecked_tail()
    {
        ReviewPull[] pulls = [Pull(1), Pull(2), Conflicted(3), Conflicted(4), Changes(5), Pull(6), Pull(7), Pull(8), Pull(9)];
        var summaries = new Dictionary<string, ReviewSummary>(StringComparer.Ordinal) { [Key(1)] = Red(_now.AddDays(-3)) };
        var open = Inputs(pulls: pulls, summaries: summaries, now: _now);
        var facts = open.Facts with { Summaries = open.Facts.Summaries.Where(static e => e.Key != Key(9)).ToDictionary(static e => e.Key, static e => e.Value, StringComparer.Ordinal) };

        var truth = ProjectTruths.Build(open with { Facts = facts });

        Assert.Equal((ProjectTruthLevel.AtRisk, "At risk: 1 of 9 PRs red for 3 d, 2 with conflicts, 1 changes requested, 1 not checked yet"), (truth.Health!.Level, truth.Health.Reason));
    }

    [Fact]
    public void Health_without_a_red_pr_names_the_blocked_ones_and_a_pr_that_is_unknown_to_the_forge_counts_for_neither()
    {
        ReviewPull[] pulls = [Conflicted(1), Changes(2), Pull(3) with { Mergeable = null }, Pull(4) with { Reviews = [new ReviewedAt("sam", "abc", NewCommits: 0, State: null)] }, Pull(5), Pull(6), Pull(7), Pull(8), Pull(9)];

        var truth = ProjectTruths.Build(Inputs(pulls: pulls));

        Assert.Equal((ProjectTruthLevel.AtRisk, "At risk: 2 of 9 PRs blocked: 1 with conflicts, 1 changes requested"), (truth.Health!.Level, truth.Health.Reason));
    }

    [Fact]
    public void Health_counts_a_pr_that_is_red_and_conflicted_once_and_a_draft_not_at_all()
    {
        ReviewPull[] pulls = [Conflicted(1), Pull(2), Pull(3) with { Draft = true, Mergeable = false }];
        var summaries = new Dictionary<string, ReviewSummary>(StringComparer.Ordinal) { [Key(1)] = Red(_now.AddHours(-2)) };

        var truth = ProjectTruths.Build(Inputs(pulls: pulls, summaries: summaries, now: _now));

        Assert.Equal((ProjectTruthLevel.OffTrack, "Off track: 1 of 2 PRs red for 2 h, 1 with conflicts"), (truth.Health!.Level, truth.Health.Reason));
    }

    [Fact]
    public void Health_at_risk_adds_the_unknown_prs_to_its_reason()
    {
        ReviewPull[] pulls = [.. Enumerable.Range(1, 9).Select(static n => Pull(n))];
        var summaries = Enumerable.Range(1, 3).ToDictionary(static n => Key(n), _ => Red(_now.AddDays(-2)), StringComparer.Ordinal);
        var open = Inputs(pulls: pulls, summaries: summaries);
        var facts = open.Facts with { Summaries = open.Facts.Summaries.Where(static e => e.Key != Key(8) && e.Key != Key(9)).ToDictionary(static e => e.Key, static e => e.Value, StringComparer.Ordinal) };

        var truth = ProjectTruths.Build(open with { Facts = facts });

        Assert.Equal((ProjectTruthLevel.AtRisk, "At risk: 3 of 9 PRs red, 2 not checked yet"), (truth.Health!.Level, truth.Health.Reason));
    }

    [Fact]
    public void Health_is_unknown_when_no_pr_is_red_but_some_have_no_ci_summary()
    {
        var truth = ProjectTruths.Build(Inputs(pulls: [Pull(1), Pull(2)], noSummaries: true));

        Assert.Equal((ProjectTruthLevel.Unknown, "CI not checked yet for 2 of 2 PRs"), (truth.Health!.Level, truth.Health.Reason));
    }

    [Fact]
    public void Health_unknown_counts_only_the_unchecked_prs()
    {
        var open = Inputs(pulls: [.. Enumerable.Range(1, 5).Select(static n => Pull(n))]);
        var facts = open.Facts with { Summaries = open.Facts.Summaries.Where(static e => e.Key != Key(4) && e.Key != Key(5)).ToDictionary(static e => e.Key, static e => e.Value, StringComparer.Ordinal) };

        var truth = ProjectTruths.Build(open with { Facts = facts });

        Assert.Equal((ProjectTruthLevel.Unknown, "CI not checked yet for 2 of 5 PRs"), (truth.Health!.Level, truth.Health.Reason));
    }

    [Fact]
    public void Precedence_is_draft_over_ci_red_over_waiting_over_unknown_over_ready()
    {
        var pulls = new[] { Pull(1, draft: true), Pull(2, requested: ["joi"]), Pull(3), Pull(4), Pull(5, draft: true, requested: ["joi"]) };
        var open = Inputs(pulls: pulls, summaries: new Dictionary<string, ReviewSummary>(StringComparer.Ordinal) { [Key(1)] = Red(_now), [Key(2)] = Red(_now), [Key(5)] = Red(_now) });
        var facts = open.Facts with { Summaries = open.Facts.Summaries.Where(static e => e.Key != Key(3)).ToDictionary(static e => e.Key, static e => e.Value, StringComparer.Ordinal) };

        var truth = ProjectTruths.Build(open with { Facts = facts });

        ProjectPrState[] states = [.. truth.OpenPrs.OrderBy(static p => p.Number).Select(static p => p.State)];
        Assert.Equal(_precedence, states);
    }

    [Fact]
    public void Health_is_off_track_when_half_the_prs_or_more_are_red()
    {
        var summaries = new Dictionary<string, ReviewSummary>(StringComparer.Ordinal) { [Key(1)] = Red(_now), [Key(2)] = Red(_now) };

        var truth = ProjectTruths.Build(Inputs(pulls: [Pull(1), Pull(2), Pull(3)], summaries: summaries));

        Assert.Equal((ProjectTruthLevel.OffTrack, "Off track: 2 of 3 PRs red"), (truth.Health!.Level, truth.Health.Reason));
    }

    [Fact]
    public void A_project_with_only_draft_prs_has_no_health()
    {
        var truth = ProjectTruths.Build(Inputs(pulls: [Pull(1, draft: true), Pull(2, draft: true)]));

        Assert.Null(truth.Health);
    }

    [Fact]
    public void Drafts_are_not_open_prs_in_the_on_track_sentence()
    {
        var truth = ProjectTruths.Build(Inputs(pulls: [Pull(1), Pull(2, draft: true)]));

        Assert.Equal((ProjectTruthLevel.OnTrack, "On track: 1 open PR, none red"), (truth.Health!.Level, truth.Health.Reason));
    }

    [Fact]
    public void Drafts_are_left_out_of_the_red_ratio_and_the_red_count_reads_the_same_set()
    {
        // 1 red of 3 open (the draft is not open): a third is at risk. Counting the draft would make it 1 of 4.
        var summaries = new Dictionary<string, ReviewSummary>(StringComparer.Ordinal) { [Key(1)] = Red(_now) };

        var truth = ProjectTruths.Build(Inputs(pulls: [Pull(1), Pull(2), Pull(3), Pull(4, draft: true)], summaries: summaries));

        Assert.Equal((ProjectTruthLevel.AtRisk, "At risk: 1 of 3 PRs red"), (truth.Health!.Level, truth.Health.Reason));
    }

    [Fact]
    public void One_red_pr_and_a_draft_is_one_of_one_red_not_one_of_two()
    {
        var summaries = new Dictionary<string, ReviewSummary>(StringComparer.Ordinal) { [Key(1)] = Red(_now) };

        var truth = ProjectTruths.Build(Inputs(pulls: [Pull(1), Pull(2, draft: true)], summaries: summaries));

        Assert.Equal((ProjectTruthLevel.OffTrack, "Off track: 1 of 1 PR red"), (truth.Health!.Level, truth.Health.Reason));
    }

    [Fact]
    public void Health_text_says_PR_for_one_on_every_branch()
    {
        var red = new Dictionary<string, ReviewSummary>(StringComparer.Ordinal) { [Key(1)] = Red(_now) };

        var reasons = (
            ProjectTruths.Build(Inputs(pulls: [Pull(1)], noSummaries: true)).Health!.Reason,
            ProjectTruths.Build(Inputs(pulls: [Pull(1)])).Health!.Reason,
            ProjectTruths.Build(Inputs(pulls: [Pull(1)], summaries: red)).Health!.Reason);

        Assert.Equal(("CI not checked yet for 1 of 1 PR", "On track: 1 open PR, none red", "Off track: 1 of 1 PR red"), reasons);
    }

    [Fact]
    public void A_force_pushed_green_pr_reads_not_checked_yet_not_unknown()
    {
        var stale = new ReviewSummary("old", ReviewCi.Pass, RequiredOpen: null, _now.AddDays(-1));

        var truth = ProjectTruths.Build(Inputs(pulls: [Pull(1, sha: "new")], summaries: new Dictionary<string, ReviewSummary>(StringComparer.Ordinal) { [Key(1)] = stale }));

        Assert.Equal((ProjectTruthLevel.Unknown, "CI not checked yet for 1 of 1 PR"), (truth.Health!.Level, truth.Health.Reason));
    }

    [Fact]
    public void Health_unknown_tail_counts_one_unchecked_pr_in_the_singular_form()
    {
        var open = Inputs(pulls: [Pull(1), Pull(2), Pull(3)], summaries: new Dictionary<string, ReviewSummary>(StringComparer.Ordinal) { [Key(1)] = Red(_now) });
        var facts = open.Facts with { Summaries = open.Facts.Summaries.Where(static e => e.Key != Key(3)).ToDictionary(static e => e.Key, static e => e.Value, StringComparer.Ordinal) };

        var truth = ProjectTruths.Build(open with { Facts = facts });

        Assert.Equal("At risk: 1 of 3 PRs red, 1 not checked yet", truth.Health!.Reason);
    }

    [Fact]
    public void A_pr_whose_requested_reviewers_include_the_owner_is_blocked_on_you()
    {
        var truth = ProjectTruths.Build(Inputs(pulls: [Pull(1, requested: ["sam", "joi"]), Pull(2)], owner: "joi"));

        Assert.True(truth.BlockedOnYou);
        Assert.Equal(_firstWaitsOnYou, truth.OpenPrs.OrderBy(static p => p.Number).Select(static p => p.WaitsOnYou).ToArray());
    }

    [Fact]
    public void A_red_pr_that_asks_the_owner_for_review_is_still_blocked_on_you()
    {
        var truth = ProjectTruths.Build(Inputs(pulls: [Pull(1, requested: ["joi"])], summaries: new Dictionary<string, ReviewSummary>(StringComparer.Ordinal) { [Key(1)] = Red(_now) }, owner: "joi"));

        var pr = truth.OpenPrs.Single();
        Assert.Equal((ProjectPrState.CiRed, true, true), (pr.State, pr.WaitsOnYou, truth.BlockedOnYou));
    }

    [Fact]
    public void A_pr_that_asks_only_another_reviewer_is_not_blocked_on_you()
    {
        var truth = ProjectTruths.Build(Inputs(pulls: [Pull(1, requested: ["sam"])], owner: "joi"));

        Assert.Equal((false, false), (truth.OpenPrs.Single().WaitsOnYou, truth.BlockedOnYou));
    }

    [Fact]
    public void A_pr_with_commits_after_the_owners_review_is_not_blocked_on_you_since_only_requested_reviewers_count()
    {
        var truth = ProjectTruths.Build(Inputs(pulls: [Pull(1, reviews: [new ReviewedAt("joi", "old", NewCommits: 2)])], owner: "joi"));

        Assert.False(truth.BlockedOnYou);
    }

    [Fact]
    public void With_no_github_login_known_nothing_is_blocked_on_you()
    {
        var truth = ProjectTruths.Build(Inputs(pulls: [Pull(1, requested: ["joi"])], owner: null));

        Assert.Equal((false, false), (truth.OpenPrs.Single().WaitsOnYou, truth.BlockedOnYou));
    }

    [Fact]
    public void The_owner_login_matches_a_requested_reviewer_in_another_case()
    {
        var truth = ProjectTruths.Build(Inputs(pulls: [Pull(1, requested: ["Joi-Example"])], owner: "joi-example"));

        Assert.True(truth.BlockedOnYou);
    }

    [Fact]
    public void A_draft_that_asks_the_owner_for_review_is_not_blocked_on_you_since_drafts_are_not_open()
    {
        var truth = ProjectTruths.Build(Inputs(pulls: [Pull(1, draft: true, requested: ["joi"])], owner: "joi"));

        var pr = truth.OpenPrs.Single();
        Assert.Equal((ProjectPrState.Draft, false, false), (pr.State, pr.WaitsOnYou, truth.BlockedOnYou));
    }
}
