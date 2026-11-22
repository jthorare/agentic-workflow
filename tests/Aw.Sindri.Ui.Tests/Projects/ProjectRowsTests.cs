using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Projects;
using Aw.Sindri.Ui.ViewModels.Projects;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>The Projects rows' rules (issue 3717, frames round 11, decisions comment): each view's projects, each sort, the groups' order, and a row's cells.</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectRowsTests
{
    private static readonly DateOnly _today = new(2026, 11, 14);

    private static ProjectCardView Card(string name, string origin = "led", string? state = "inProgress", string? target = null, ProjectTruthView? truth = null, int? daysAgo = null) =>
        new("prj_" + name, origin, name, Description: null, state, StateSetByOwner: false, Health: null, Lead: null, target, OpenIssues: null, Counting: false,
            OpenPrs: null, PrScope: null, daysAgo is { } days ? new DateTimeOffset(2026, 11, 14, 10, 42, 0, TimeSpan.Zero).AddDays(-days) : null, FinishedAt: null,
            Repos: [], Links: [], Notes: null, Synced: true, Version: 1, truth);

    private static ProjectTruthView Truth(string? level, bool? blockedOnYou = null, params ProjectTruthPrView[] prs) =>
        new(prs, OpenItems: 0, DoneItems: 0, TotalItems: 0, RunningAgents: 0, level is null ? null : new ProjectTruthHealthView(level, "served reason"), blockedOnYou);

    private static ProjectTruthPrView Pr(string state, string repo = "acme/api", string? waitingOn = null, bool? waitsOnYou = null) =>
        new("https://github.com/" + repo + "/pull/1", repo, 1, "A PR", state, DecidedAt: null, waitingOn, waitsOnYou);

    private static List<string> Names(IEnumerable<ProjectCardView> cards) => [.. cards.Select(c => c.Name)];

    private static List<string> InView(ProjectView view, params ProjectCardView[] cards) => Names(cards.Where(c => ProjectRows.InView(view, c)));

    [Fact]
    public void Needs_me_holds_only_projects_the_server_flags_as_blocked_on_you()
    {
        var flagged = Card("Flagged", truth: Truth("atRisk", blockedOnYou: true));
        var unflagged = Card("Unflagged", truth: Truth("offTrack", blockedOnYou: false));
        var older = Card("Older server", truth: Truth("offTrack"));
        var none = Card("No truth");

        Assert.Equal(["Flagged"], InView(ProjectView.NeedsMe, flagged, unflagged, older, none), StringComparer.Ordinal);
    }

    [Fact]
    public void My_work_holds_led_and_hand_made_projects_and_leaves_attached_out()
    {
        Assert.Equal(["Led", "By hand"], InView(ProjectView.MyWork, Card("Led"), Card("By hand", origin: "handMade"), Card("Attached", origin: "attached")), StringComparer.Ordinal);
    }

    [Fact]
    public void Projects_holds_every_open_project_and_Triage_holds_none()
    {
        ProjectCardView[] cards = [Card("Led"), Card("Attached", origin: "attached"), Card("By hand", origin: "handMade")];

        Assert.Equal(["Led", "Attached", "By hand"], InView(ProjectView.All, cards), StringComparer.Ordinal);
        Assert.Empty(InView(ProjectView.Triage, cards));
    }

    [Fact]
    public void Last_activity_sorts_newest_first_then_by_name()
    {
        var sorted = ProjectRows.Sorted([Card("Old", daysAgo: 9), Card("Zebra", daysAgo: 1), Card("apple", daysAgo: 1), Card("New", daysAgo: 0)], ProjectSort.Activity);

        Assert.Equal(["New", "apple", "Zebra", "Old"], Names(sorted), StringComparer.Ordinal);
    }

    [Fact]
    public void Projects_with_no_last_activity_go_after_the_others_by_name()
    {
        var sorted = ProjectRows.Sorted([Card("Zulu"), Card("Old", daysAgo: 30), Card("alpha"), Card("New", daysAgo: 0)], ProjectSort.Activity);

        Assert.Equal(["New", "Old", "alpha", "Zulu"], Names(sorted), StringComparer.Ordinal);
    }

    [Fact]
    public void Target_date_sorts_soonest_first_and_puts_projects_without_one_last()
    {
        var sorted = ProjectRows.Sorted([Card("None"), Card("Late", target: "2026-12-20"), Card("Soon", target: "2026-11-20"), Card("Bad", target: "soon")], ProjectSort.Target);

        Assert.Equal(["Soon", "Late", "Bad", "None"], Names(sorted), StringComparer.Ordinal);
    }

    [Fact]
    public void Name_sorts_A_to_Z_whatever_the_case()
    {
        Assert.Equal(["apple", "Mango", "Zebra"], Names(ProjectRows.Sorted([Card("Zebra"), Card("apple"), Card("Mango")], ProjectSort.Name)), StringComparer.Ordinal);
    }

    [Fact]
    public void Health_groups_follow_the_fixed_order_with_CI_not_checked_and_No_open_PRs_last()
    {
        ProjectCardView[] cards =
        [
            Card("Fine", truth: Truth("onTrack")),
            Card("Nothing open", truth: Truth(level: null)),
            Card("Unchecked", truth: Truth("unknown")),
            Card("Risky", truth: Truth("atRisk")),
            Card("Older server"),
            Card("Red", truth: Truth("offTrack")),
        ];

        var groups = ProjectRows.Grouped(cards, ProjectGrouping.Health, factsMissing: false);

        Assert.Equal(["Off track", "At risk", "CI not checked", "On track", "No open PRs"], groups.Select(g => g.Heading), StringComparer.Ordinal);
        Assert.Equal(["Nothing open", "Older server"], Names(groups[^1].Cards), StringComparer.Ordinal);
    }

    [Fact]
    public void A_health_group_with_no_project_is_left_out()
    {
        var groups = ProjectRows.Grouped([Card("Fine", truth: Truth("onTrack")), Card("Red", truth: Truth("offTrack"))], ProjectGrouping.Health, factsMissing: false);

        Assert.Equal(["Off track", "On track"], groups.Select(g => g.Heading), StringComparer.Ordinal);
    }

    [Fact]
    public void Missing_facts_show_no_health_or_repo_groups_but_state_groups()
    {
        ProjectCardView[] cards = [Card("A", state: "planned", truth: Truth(level: null)), Card("B", truth: Truth(level: null))];

        Assert.Equal(["In progress", "Planned"], ProjectRows.Grouped(cards, ProjectGrouping.Health, factsMissing: true).Select(g => g.Heading), StringComparer.Ordinal);
        Assert.Equal(["In progress", "Planned"], ProjectRows.Grouped(cards, ProjectGrouping.Repo, factsMissing: true).Select(g => g.Heading), StringComparer.Ordinal);
        Assert.Equal(string.Empty, Assert.Single(ProjectRows.Grouped(cards, ProjectGrouping.None, factsMissing: true)).Heading);
    }

    [Theory]
    [InlineData("sindri", "failing", true)]
    [InlineData("github", "failing", false)]
    [InlineData("sindri", "ok", false)]
    public void Missing_facts_are_read_from_a_sindri_freshness_row(string source, string state, bool missing)
    {
        var list = new ProjectList(Empty: null, Viewer: null, [new ProjectFreshnessView(source, state, "text", At: null)], [], [], []);

        Assert.Equal(missing, ProjectRows.FactsMissing(list));
    }

    [Fact]
    public void State_groups_keep_the_known_states_order_then_others()
    {
        var groups = ProjectRows.Grouped([Card("S", state: "someday"), Card("P", state: "paused"), Card("I", state: "inProgress")], ProjectGrouping.State, factsMissing: false);

        Assert.Equal(["In progress", "Paused", "Someday"], groups.Select(g => g.Heading), StringComparer.Ordinal);
    }

    [Fact]
    public void Repo_groups_show_a_project_under_each_repo_of_its_PRs_that_are_not_drafts()
    {
        ProjectCardView[] cards =
        [
            Card("Both", truth: Truth("onTrack", blockedOnYou: null, Pr("ready", "acme/web"), Pr("ciRed", "acme/api"))),
            Card("Drafts only", truth: Truth(level: null, blockedOnYou: null, Pr("draft", "acme/api"))),
        ];

        var groups = ProjectRows.Grouped(cards, ProjectGrouping.Repo, factsMissing: false);

        Assert.Equal(["acme/api", "acme/web", "No open PRs"], groups.Select(g => g.Heading), StringComparer.Ordinal);
        Assert.Equal(["Both"], Names(groups[0].Cards), StringComparer.Ordinal);
        Assert.Equal(["Drafts only"], Names(groups[2].Cards), StringComparer.Ordinal);
    }

    [Fact]
    public void No_grouping_is_one_list_with_no_head()
    {
        var group = Assert.Single(ProjectRows.Grouped([Card("A"), Card("B")], ProjectGrouping.None, factsMissing: false));

        Assert.Equal(string.Empty, group.Heading);
    }

    [Fact]
    public void A_target_before_today_reads_passed()
    {
        Assert.Equal(("Nov 12 passed", true), ProjectRows.Target("2026-11-12", _today));
    }

    [Theory]
    [InlineData("2026-11-14", "Nov 14")]
    [InlineData("2026-12-07", "Dec 7")]
    [InlineData("next week", "next week")]
    public void A_target_today_or_later_reads_its_day(string target, string words)
    {
        Assert.Equal((words, false), ProjectRows.Target(target, _today));
    }

    [Fact]
    public void The_PR_glyphs_go_red_waiting_unknown_ready_then_drafts_after_a_hairline()
    {
        var glyphs = ProjectRows.Glyphs(Truth("atRisk", blockedOnYou: null, Pr("draft"), Pr("ready"), Pr("ciRed"), Pr("ciRed"), Pr("unknown"), Pr("waiting")));

        Assert.Equal(["ciRed 2", "waiting 1", "unknown 1", "ready 1", "draft 1"], glyphs.Select(g => g.State + " " + g.CountText), StringComparer.Ordinal);
        Assert.True(glyphs[^1].AfterHairline);
        Assert.False(Assert.Single(ProjectRows.Glyphs(Truth(level: null, blockedOnYou: null, Pr("draft")))).AfterHairline);
    }

    [Fact]
    public void Blocked_on_is_you_when_the_server_says_so_else_the_first_reviewer()
    {
        Assert.Equal("you", ProjectRows.BlockedOn(Truth("atRisk", blockedOnYou: true, Pr("waiting", waitingOn: "sam"))));
        Assert.Equal("sam", ProjectRows.BlockedOn(Truth("atRisk", blockedOnYou: null, Pr("ciRed"), Pr("waiting", waitingOn: "sam, lee"))));
        Assert.Equal(string.Empty, ProjectRows.BlockedOn(Truth("onTrack", blockedOnYou: null, Pr("ready"))));
    }

    [Fact]
    public void A_PR_that_waits_on_you_makes_the_project_blocked_on_you()
    {
        var truth = Truth("atRisk", blockedOnYou: null, Pr("waiting", waitingOn: "sam, joi", waitsOnYou: true));

        Assert.Equal(("you", true), (ProjectRows.BlockedOn(truth), ProjectRows.WaitsOnYou(truth)));
        Assert.False(ProjectRows.WaitsOnYou(Truth("atRisk", blockedOnYou: false, Pr("waiting", waitingOn: "sam", waitsOnYou: false))));
    }
}
