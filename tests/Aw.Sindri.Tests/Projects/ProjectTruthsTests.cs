using Aw.Kernel.Testing;
using Aw.Sindri.Core.Projects.Cards;
using Aw.Sindri.Core.Projects.Truth;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Projects;

/// <summary>A project's truth from Sindri's own data (issue 3718): sources from links, PR states, items, agents and the derived health. Pure; no Linear anywhere.</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectTruthsTests
{
    private const string Api = "acme/api";
    private const string Web = "acme/web";
    private static readonly DateTimeOffset _now = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly ProjectSources _apiOnly = new([Api], []);

    private static ReviewPull Pull(int number, string repo = Api, bool draft = false, string[]? requested = null, string sha = "abc", ReviewedAt[]? reviews = null) =>
        new(repo, number, $"PR {number}", "sam", draft, _now.AddDays(-5), _now.AddDays(-1), sha, requested ?? [], reviews ?? [], ReviewersRead: true, [], $"https://github.com/{repo}/pull/{number}");

    private static string Key(int number, string repo = Api) => $"{repo}#{number}";

    // every pull gets a passing summary of its head unless 'summaries' overrides it or 'unchecked' leaves it out
    private static ProjectTruthInputs Inputs(
        ProjectSources? sources = null,
        IReadOnlyList<ReviewPull>? pulls = null,
        IReadOnlyDictionary<string, ReviewSummary>? summaries = null,
        IReadOnlyList<ProjectTruthItem>? items = null,
        IReadOnlyList<ProjectTruthAgent>? agents = null,
        bool noSummaries = false)
    {
        var all = new Dictionary<string, ReviewSummary>();
        foreach (var pull in pulls ?? [])
        {
            if (!noSummaries)
            {
                all[Key(pull.Number, pull.Repo)] = new ReviewSummary(pull.HeadSha!, ReviewCi.Pass, RequiredOpen: null, _now.AddDays(-1));
            }
        }

        foreach (var (key, summary) in summaries ?? new Dictionary<string, ReviewSummary>())
        {
            all[key] = summary;
        }

        return new(sources ?? _apiOnly, new ProjectTruthFacts(pulls ?? [], all, items ?? [], agents ?? []));
    }

    private static ReviewSummary Red(DateTimeOffset at) => new("abc", ReviewCi.Fail, RequiredOpen: null, at);

    [Fact]
    public void Sources_come_from_links_that_name_a_github_pr_or_repo_and_from_the_linear_and_profile_repos()
    {
        var sources = ProjectTruths.SourcesOf(
            [new ProjectLink("a", "https://github.com/acme/web/pull/7"), new ProjectLink("b", "https://github.com/acme/api"), new ProjectLink("c", "https://example.com/plan")],
            linearRepos: ["acme/infra"],
            profileRepos: ["acme/api"]);

        Assert.Equal(new[] { "https://github.com/acme/web/pull/7" }, sources.PullUrls.ToArray());
        Assert.Equal(new[] { "acme/api", "acme/infra", "acme/web" }, sources.Repos.OrderBy(static r => r, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void Sources_list_a_repo_once_when_a_link_and_the_profile_both_name_it()
    {
        var sources = ProjectTruths.SourcesOf([new ProjectLink("b", "https://github.com/acme/api")], linearRepos: [], profileRepos: [Api]);

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

        Assert.Equal(new[] { 7 }, truth.OpenPrs.Select(static p => p.Number).ToArray());
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

        var truth = ProjectTruths.Build(Inputs(pulls: [Pull(1)], summaries: new Dictionary<string, ReviewSummary> { [Key(1)] = Red(at) }));

        var pr = truth.OpenPrs.Single();
        Assert.Equal((ProjectPrState.CiRed, (DateTimeOffset?)at), (pr.State, pr.DecidedAt));
    }

    [Fact]
    public void A_summary_of_an_older_head_is_not_a_check_of_this_head_so_the_pr_is_unknown()
    {
        var stale = new ReviewSummary("old", ReviewCi.Fail, RequiredOpen: null, _now.AddDays(-9));

        var truth = ProjectTruths.Build(Inputs(pulls: [Pull(1, sha: "new")], summaries: new Dictionary<string, ReviewSummary> { [Key(1)] = stale }));

        Assert.Equal(ProjectPrState.Unknown, truth.OpenPrs.Single().State);
    }

    [Fact]
    public void A_pr_with_no_summary_at_all_is_unknown_never_ready_and_has_no_decided_at()
    {
        var truth = ProjectTruths.Build(Inputs(pulls: [Pull(1)], noSummaries: true));

        var pr = truth.OpenPrs.Single();
        Assert.Equal((ProjectPrState.Unknown, (DateTimeOffset?)null), (pr.State, pr.DecidedAt));
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
    public void Ci_red_wins_over_waiting()
    {
        var truth = ProjectTruths.Build(Inputs(pulls: [Pull(1, requested: ["joi"])], summaries: new Dictionary<string, ReviewSummary> { [Key(1)] = Red(_now) }));

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
        var agents = new[] { new ProjectTruthAgent(Api, Running: true), new ProjectTruthAgent(Api, Running: false), new ProjectTruthAgent(Web, Running: true), new ProjectTruthAgent(null, Running: true) };

        var truth = ProjectTruths.Build(Inputs(agents: agents));

        Assert.Equal(1, truth.RunningAgents);
    }

    [Fact]
    public void A_project_with_no_prs_and_no_items_has_no_health()
    {
        Assert.Null(ProjectTruths.Build(Inputs()).Health);
    }

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
        var summaries = Enumerable.Range(1, 3).ToDictionary(static n => Key(n), _ => Red(_now.AddDays(-2)));

        var truth = ProjectTruths.Build(Inputs(pulls: pulls, summaries: summaries));

        Assert.Equal((ProjectTruthLevel.AtRisk, "At risk: 3 of 9 PRs red"), (truth.Health!.Level, truth.Health.Reason));
    }

    [Fact]
    public void Health_at_risk_adds_the_unknown_prs_to_its_reason()
    {
        ReviewPull[] pulls = [.. Enumerable.Range(1, 9).Select(static n => Pull(n))];
        var summaries = Enumerable.Range(1, 3).ToDictionary(static n => Key(n), _ => Red(_now.AddDays(-2)));
        var open = Inputs(pulls: pulls, summaries: summaries);
        var facts = open.Facts with { Summaries = open.Facts.Summaries.Where(static e => e.Key != Key(8) && e.Key != Key(9)).ToDictionary(static e => e.Key, static e => e.Value) };

        var truth = ProjectTruths.Build(open with { Facts = facts });

        Assert.Equal((ProjectTruthLevel.AtRisk, "At risk: 3 of 9 PRs red, 2 unknown"), (truth.Health!.Level, truth.Health.Reason));
    }

    [Fact]
    public void Health_is_unknown_when_no_pr_is_red_but_some_have_no_ci_summary()
    {
        var truth = ProjectTruths.Build(Inputs(pulls: [Pull(1), Pull(2)], noSummaries: true));

        Assert.Equal((ProjectTruthLevel.Unknown, "CI unknown for 2 of 2 PRs"), (truth.Health!.Level, truth.Health.Reason));
    }

    [Fact]
    public void Health_unknown_counts_only_the_unchecked_prs()
    {
        var open = Inputs(pulls: [.. Enumerable.Range(1, 5).Select(static n => Pull(n))]);
        var facts = open.Facts with { Summaries = open.Facts.Summaries.Where(static e => e.Key != Key(4) && e.Key != Key(5)).ToDictionary(static e => e.Key, static e => e.Value) };

        var truth = ProjectTruths.Build(open with { Facts = facts });

        Assert.Equal((ProjectTruthLevel.Unknown, "CI unknown for 2 of 5 PRs"), (truth.Health!.Level, truth.Health.Reason));
    }

    [Fact]
    public void Precedence_is_draft_over_ci_red_over_waiting_over_unknown_over_ready()
    {
        var pulls = new[] { Pull(1, draft: true), Pull(2, requested: ["joi"]), Pull(3), Pull(4), Pull(5, draft: true, requested: ["joi"]) };
        var open = Inputs(pulls: pulls, summaries: new Dictionary<string, ReviewSummary> { [Key(1)] = Red(_now), [Key(2)] = Red(_now), [Key(5)] = Red(_now) });
        var facts = open.Facts with { Summaries = open.Facts.Summaries.Where(static e => e.Key != Key(3)).ToDictionary(static e => e.Key, static e => e.Value) };

        var truth = ProjectTruths.Build(open with { Facts = facts });

        Assert.Equal(
            new[] { ProjectPrState.Draft, ProjectPrState.CiRed, ProjectPrState.Unknown, ProjectPrState.Ready, ProjectPrState.Draft },
            truth.OpenPrs.OrderBy(static p => p.Number).Select(static p => p.State).ToArray());
    }

    [Fact]
    public void Health_is_off_track_when_half_the_prs_or_more_are_red()
    {
        var summaries = new Dictionary<string, ReviewSummary> { [Key(1)] = Red(_now), [Key(2)] = Red(_now) };

        var truth = ProjectTruths.Build(Inputs(pulls: [Pull(1), Pull(2), Pull(3)], summaries: summaries));

        Assert.Equal((ProjectTruthLevel.OffTrack, "Off track: 2 of 3 PRs red"), (truth.Health!.Level, truth.Health.Reason));
    }
}
