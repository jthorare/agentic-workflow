using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Home;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.Tests.Home;

/// <summary>The Home page as rows (home spec §7.1, U1) on the API's own golden pages: tab counts by kind, a split's rows, the row map, paging.</summary>
[Lane(TestLane.Unit)]
public sealed class HomeRowsViewModelTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 12, 9, 10, 0, TimeSpan.Zero);

    private static HomeView Page(string scenario) => AwJson.Deserialize<HomeView>(Encoding.UTF8.GetBytes(GoldenFiles.Api("home", scenario)), SindriApiClient.Json).Value;

    private static HomeRowsViewModel Shown(string scenario = "fixture")
    {
        var home = new HomeRowsViewModel();
        home.Show(Page(scenario), _now);
        return home;
    }

    [Fact]
    public void Each_tab_counts_the_open_rows_of_its_kinds()
    {
        var home = Shown();

        Assert.Equal(
            [(HomeSplit.All, "All", 6), (HomeSplit.Reviews, "Reviews", 3), (HomeSplit.Messages, "Messages", 1), (HomeSplit.Work, "Work", 0), (HomeSplit.Agents, "Agents", 2)],
            home.Tabs.Select(t => (t.Split, t.Label, t.Count)));
        Assert.Equal(HomeSplit.All, Assert.Single(home.Tabs, t => t.IsSelected).Split);
    }

    [Fact]
    public void All_shows_one_Needs_you_group_in_the_served_order()
    {
        var home = Shown();

        var group = Assert.Single(home.Groups);
        Assert.Equal("Needs you", group.Heading);
        Assert.Equal(Page("fixture").NeedsYou.Select(r => r.Id), group.Rows.Select(r => r.Key), StringComparer.Ordinal);
        Assert.Null(home.EmptyLine);
    }

    [Fact]
    public void A_split_shows_only_its_kinds_and_keeps_every_count()
    {
        var home = Shown();

        home.Select(HomeSplit.Agents);

        Assert.Equal(["lane:recycle:meta", "lane:recycle:ideas"], Assert.Single(home.Groups).Rows.Select(r => r.Key), StringComparer.Ordinal);
        Assert.Equal([6, 3, 1, 0, 2], home.Tabs.Select(t => t.Count));
        Assert.Equal(HomeSplit.Agents, Assert.Single(home.Tabs, t => t.IsSelected).Split);
    }

    [Fact]
    public void A_split_with_no_rows_says_so_in_one_line()
    {
        var home = Shown();

        home.Select(HomeSplit.Work);

        Assert.Empty(Assert.Single(home.Groups).Rows);   // the Needs you head stays, with no row
        Assert.Equal("Needs you", home.Groups[0].Heading);
        Assert.Equal("Nothing in Work", home.EmptyLine);
    }

    [Fact]
    public void The_empty_Messages_tab_says_Slack_comes_later()
    {
        var home = Shown("empty");

        home.Select(HomeSplit.Messages);

        Assert.Equal("Slack messages arrive in a later release.", home.EmptyLine);
    }

    [Fact]
    public void A_PR_row_leads_with_its_number_and_names_the_repo_as_its_object()
    {
        var row = Shown().Groups[0].Rows.Single(r => r.Key == "gh:review:jthorare/agentic-workflow#3120");

        Assert.Equal("#3120", row.Id);
        Assert.Equal("Review requested: PR 3120", row.Title);
        Assert.Equal("pr", row.Kind);
        Assert.Equal(new RowFact(RowFactKind.Text, "blocks 2"), row.Facts[HomeRowItem.Why]);
        Assert.Equal(new RowFact(RowFactKind.Ref, "repo demo"), row.Facts[HomeRowItem.Object]);
        Assert.Equal(new RowFact(RowFactKind.Person, "Sam"), row.Facts[HomeRowItem.From]);
        Assert.Equal("1h 40m", row.Age);
        Assert.Null(row.WaitsOn);
    }

    [Fact]
    public void A_lane_row_has_no_id_and_keeps_its_served_facts()
    {
        var row = Shown().Groups[0].Rows[0];

        Assert.Equal(string.Empty, row.Id);
        Assert.Equal("session", row.Kind);
        Assert.Equal(RowGlyph.NeedsYou, RowGlyphs.For(row.Kind, row.StateClass, row.StateKey));
        Assert.Equal(new RowFact(RowFactKind.Ref, "session meta"), row.Facts[HomeRowItem.Object]);
        Assert.Equal(new RowFact(RowFactKind.Person, "lane meta"), row.Facts[HomeRowItem.From]);
    }

    [Fact]
    public void The_why_cell_says_blocks_N_from_2_up_and_the_reason_word_under_that()
    {
        var rows = Shown().Groups[0].Rows;

        Assert.Equal(new RowFact(RowFactKind.Text, "blocks 2"), rows.Single(r => r.Key == "gh:ready:jthorare/agentic-workflow#3099").Facts[HomeRowItem.Why]);
        Assert.Equal(new RowFact(RowFactKind.Text, "ready"), rows.Single(r => r.Key == "gh:ready:jthorare/agentic-workflow#3101").Facts[HomeRowItem.Why]);   // served as "blocks 1"
        Assert.Equal(new RowFact(RowFactKind.Text, "DM"), rows.Single(r => r.Row.Kind == "dm").Facts[HomeRowItem.Why]);
    }

    [Fact]
    public void A_ready_PR_draws_the_open_PR_ring_and_a_message_the_bubble()
    {
        var rows = Shown().Groups[0].Rows;

        var ready = rows.First(r => r.Row.Kind == "pr-ready-merge");
        var dm = rows.First(r => r.Row.Kind == "dm");
        Assert.Equal(RowGlyph.PrOpen, RowGlyphs.For(ready.Kind, ready.StateClass, ready.StateKey));
        Assert.Equal(RowGlyph.Message, RowGlyphs.For(dm.Kind, dm.StateClass, dm.StateKey));
    }

    [Fact]
    public void A_later_page_adds_its_rows_once()
    {
        var home = Shown();
        var before = home.Groups[0].Rows.Count;

        home.Add(Page("fixture"), _now);

        Assert.Equal(before, home.Groups[0].Rows.Count);
        Assert.Equal(6, home.Tabs[0].Count);
    }
}
