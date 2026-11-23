using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Projects;
using static Aw.Sindri.Ui.Tests.Projects.ProjectsFixture;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>
/// The Projects page's rows (issue 3717) on the mixed golden (Checkout rewrite led, Kitchen by hand, Mobile app attached; Search relaunch finished; Billing
/// cleanup hidden), with truth added: the default grouping by health, the failed truth read, the views and their counts, the empty views, sort and the row cells.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ProjectsPageRowsTests
{
    private readonly ProjectsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static JsonObject Truth(string? level, bool? blockedOnYou = null) => new()
    {
        ["openPrs"] = new JsonArray(),
        ["openItems"] = 0,
        ["doneItems"] = 0,
        ["totalItems"] = 0,
        ["runningAgents"] = 0,
        ["health"] = level is null ? null : new JsonObject { ["level"] = level, ["reason"] = "served " + level },
        ["blockedOnYou"] = blockedOnYou,
    };

    // The mixed golden with truth on its three open projects: Checkout rewrite, Kitchen, Mobile app. The finished and hidden cards carry none.
    private JsonNode Mixed(JsonObject? checkout, JsonObject? kitchen, JsonObject? mobile)
    {
        var list = Golden("mixed");
        var projects = list["projects"]!.AsArray();
        (projects[0]!["truth"], projects[1]!["truth"], projects[2]!["truth"]) = (checkout, kitchen, mobile);
        foreach (var card in list["finished"]!.AsArray().Concat(list["hidden"]!.AsArray()))
        {
            card!["truth"] = null;   // the golden's finished and hidden cards serve blockedOnYou: false, which would move the default view to Needs me
        }

        _f.Serve(list);
        return list;
    }

    private static List<string> Rows(ProjectsPageViewModel page) => [.. page.Groups.SelectMany(g => g.Cards).Select(c => c.Name)];

    private static ProjectCardViewModel Row(ProjectsPageViewModel page, string name) => page.Groups.SelectMany(g => g.Cards).First(c => c.Name == name);

    [Fact]
    public void The_page_groups_by_health_and_shows_every_project_until_told_otherwise()
    {
        var page = _f.Page();

        Assert.Equal((ProjectGrouping.Health, ProjectView.All, ProjectSort.Activity), (page.Grouping, page.View, page.Sort));
        Assert.Equal(("Health", "Last activity"), (page.GroupText, page.SortText));
    }

    [Fact]
    public async Task Needs_me_is_the_first_view_once_the_server_serves_the_flag()
    {
        _ = Mixed(Truth("offTrack", blockedOnYou: false), Truth("atRisk", blockedOnYou: true), mobile: null);

        var page = await _f.Shown(Token, defaultView: true);

        Assert.Equal((ProjectView.NeedsMe, true), (page.View, page.Tabs[0].IsOn));
        Assert.Equal(["Kitchen"], Rows(page), StringComparer.Ordinal);
    }

    [Fact]
    public async Task An_older_server_without_the_flag_opens_on_Projects()
    {
        _ = Mixed(Truth("offTrack"), Truth("atRisk"), mobile: null);

        var page = await _f.Shown(Token, defaultView: true);

        Assert.Equal(ProjectView.All, page.View);
    }

    [Fact]
    public async Task A_view_the_owner_picked_stays_when_the_flag_arrives()
    {
        _ = Mixed(checkout: null, kitchen: null, mobile: null);
        var page = await _f.Shown(Token, defaultView: true);
        page.SetView(ProjectView.All);
        _ = Mixed(Truth("offTrack", blockedOnYou: true), kitchen: null, mobile: null);

        await page.Refresh(Url, Token);

        Assert.Equal(ProjectView.All, page.View);
    }

    [Fact]
    public async Task Health_groups_are_in_order_and_the_first_carries_the_hint()
    {
        _ = Mixed(Truth("offTrack"), Truth("unknown"), mobile: null);

        var page = await _f.Shown(Token, ProjectGrouping.Health);

        Assert.Equal(["Off track", "CI not checked", "No open PRs"], page.Groups.Select(g => g.Heading), StringComparer.Ordinal);
        Assert.Equal(["derived from each project’s open PRs", string.Empty, string.Empty], page.Groups.Select(g => g.Hint), StringComparer.Ordinal);
        Assert.Equal("served offTrack", page.Groups[0].Cards[0].HealthReason);
    }

    [Fact]
    public async Task A_failed_truth_read_groups_by_state_with_the_one_line_and_no_Needs_me_count()
    {
        var list = Mixed(Truth(level: null), Truth(level: null), Truth(level: null));
        list["freshness"]!.AsArray().Add(new JsonObject { ["source"] = "sindri", ["state"] = "failing", ["text"] = "SND-REVIEWS-012: reviews.db is locked", ["at"] = null });
        _f.Serve(list);

        var page = await _f.Shown(Token, ProjectGrouping.Health);

        Assert.Equal(["In progress", "Planned", "Paused"], page.Groups.Select(g => g.Heading), StringComparer.Ordinal);
        Assert.Equal(("Health", true), (page.GroupText, page.ShowsFactsMissing));   // the Group button keeps the owner's pick
        Assert.Equal("PRs, checks and agents couldn't be read just now · grouped by state until then", page.FactsMissingText);
        Assert.DoesNotContain("couldn't be read", page.Freshness, StringComparison.Ordinal);   // said once, on this line
        Assert.All(page.Groups.SelectMany(g => g.Cards), c => Assert.False(c.ShowsFacts));
        Assert.Equal([false, true, true, true], page.Tabs.Select(t => t.ShowsCount));
        Assert.Equal("Needs me", page.Tabs[0].Name);
    }

    [Fact]
    public async Task The_tabs_count_each_view_and_Projects_counts_finished_but_not_hidden()
    {
        _ = Mixed(Truth("offTrack", blockedOnYou: true), Truth("onTrack", blockedOnYou: false), mobile: null);

        var page = await _f.Shown(Token);

        Assert.Equal([1, 2, 4, 0], page.Tabs.Select(t => t.Count));
        Assert.Equal(["1", "2", "3", "4"], page.Tabs.Select(t => t.Key), StringComparer.Ordinal);
        Assert.Equal("Projects, 4 projects", page.Tabs[2].Name);
    }

    [Fact]
    public async Task My_work_leaves_attached_projects_and_the_folds_out()
    {
        _ = Mixed(checkout: null, kitchen: null, mobile: null);
        var page = await _f.Shown(Token, ProjectGrouping.None);

        page.SetView(ProjectView.MyWork);

        Assert.Equal(["Kitchen", "Checkout rewrite"], Rows(page), StringComparer.Ordinal);   // newest activity first
        Assert.Equal((false, 0, 0), (page.ShowsFolds, page.Finished.Count, page.Hidden.Count));
        Assert.Equal([false, true, false, false], page.Tabs.Select(t => t.IsOn));
    }

    [Fact]
    public async Task Needs_me_holds_the_flagged_project()
    {
        _ = Mixed(Truth("atRisk", blockedOnYou: false), Truth("offTrack", blockedOnYou: true), mobile: null);
        var page = await _f.Shown(Token);

        page.SetView(ProjectView.NeedsMe);

        Assert.Equal(["Kitchen"], Rows(page), StringComparer.Ordinal);
        Assert.False(page.IsViewEmpty);
    }

    [Fact]
    public async Task Needs_me_shows_its_empty_state_while_no_project_is_flagged_and_its_button_opens_Projects()
    {
        _ = Mixed(Truth("offTrack"), Truth("atRisk"), mobile: null);
        var page = await _f.Shown(Token);

        page.SetView(ProjectView.NeedsMe);

        Assert.Empty(page.Groups);
        Assert.Equal((true, false, "Nothing waits on you", "Open Projects", "3"), (page.IsViewEmpty, page.HasRows, page.ViewEmptyTitle, page.ViewEmptyAction, page.ViewEmptyKey));
        page.RunViewEmptyAction();
        Assert.Equal((ProjectView.All, false), (page.View, page.IsViewEmpty));
    }

    [Fact]
    public async Task Triage_is_its_empty_state_even_with_projects_and_its_button_goes_back_to_Needs_me()
    {
        _ = Mixed(checkout: null, kitchen: null, mobile: null);
        var page = await _f.Shown(Token);

        page.SetView(ProjectView.Triage);

        Assert.Empty(page.Groups);
        Assert.Equal((true, "Triage isn’t built yet", "Back to Needs me", "1"), (page.IsViewEmpty, page.ViewEmptyTitle, page.ViewEmptyAction, page.ViewEmptyKey));
        page.RunViewEmptyAction();
        Assert.Equal(ProjectView.NeedsMe, page.View);
    }

    [Fact]
    public async Task A_new_sort_redraws_without_a_read()
    {
        var list = Mixed(checkout: null, kitchen: null, mobile: null);
        list["projects"]![1]!["target"] = "2026-01-02";
        _f.Serve(list);
        var page = await _f.Shown(Token, ProjectGrouping.None);

        page.SetSort(ProjectSort.Target);

        Assert.Equal(["Kitchen", "Checkout rewrite", "Mobile app"], Rows(page), StringComparer.Ordinal);
        Assert.Equal(("Target date", "Sort: Target date", 1), (page.SortText, page.SortName, _f.Server.ProjectsCalls));
        Assert.Equal([false, true, false], page.SortChoices.Select(c => c.IsOn));
    }

    [Fact]
    public async Task A_row_under_a_minute_old_reads_less_than_a_minute()
    {
        _ = Mixed(checkout: null, kitchen: null, mobile: null);   // Kitchen's last activity is the clock's now; Checkout rewrite's a day before

        var page = await _f.Shown(Token, ProjectGrouping.None);

        Assert.Equal(("<1m", "1d"), (Row(page, "Kitchen").AgeText, Row(page, "Checkout rewrite").AgeText));
    }

    [Fact]
    public async Task A_target_before_today_reads_passed_and_a_finished_project_shows_none()
    {
        var list = Mixed(checkout: null, kitchen: null, mobile: null);
        list["projects"]![0]!["target"] = "2025-12-30";
        list["finished"]![0]!["target"] = "2025-12-01";
        _f.Serve(list);

        var page = await _f.Shown(Token, ProjectGrouping.None);

        var checkout = Row(page, "Checkout rewrite");
        Assert.Equal(("Dec 30 passed", true), (checkout.TargetText, checkout.TargetPassed));
        Assert.Equal("Mar 1", Row(page, "Kitchen").TargetText);
        Assert.Equal(string.Empty, page.Finished.Cards[0].TargetText);
    }

    [Fact]
    public async Task A_row_reads_its_items_agents_PRs_and_who_it_is_blocked_on()
    {
        var truth = Truth("atRisk", blockedOnYou: true);
        truth["doneItems"] = 5;
        truth["totalItems"] = 12;
        truth["runningAgents"] = 2;
        truth["openPrs"] = new JsonArray(
            new JsonObject { ["url"] = "https://github.com/acme/api/pull/1", ["repo"] = "acme/api", ["number"] = 1, ["title"] = "One", ["state"] = "ciRed", ["decidedAt"] = null, ["waitingOn"] = null },
            new JsonObject { ["url"] = "https://github.com/acme/api/pull/2", ["repo"] = "acme/api", ["number"] = 2, ["title"] = "Two", ["state"] = "draft", ["decidedAt"] = null, ["waitingOn"] = null });
        _ = Mixed(truth, kitchen: null, mobile: null);

        var row = Row(await _f.Shown(Token, ProjectGrouping.None), "Checkout rewrite");

        Assert.Equal(("5/12", "2", "you", true), (row.ItemsText, row.AgentsText, row.BlockedOnText, row.IsBlockedOnYou));
        Assert.Equal(["1 CI red", "1 draft"], row.Prs.Select(p => p.Name), StringComparer.Ordinal);
        Assert.StartsWith("Checkout rewrite, served atRisk, 1 CI red, 1 draft, 5/12, you", row.RowName, StringComparison.Ordinal);
    }
}
