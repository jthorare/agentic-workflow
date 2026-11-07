using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Queue;

namespace Aw.Sindri.Ui.Tests.Linear;

/// <summary>The Work page's Linear list (Linear hub spec §3): its groups, the filters, Show more and a row's words.</summary>
[Lane(TestLane.Unit)]
public sealed class LinearWorkListTests
{
    private readonly LinearFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_list_opens_on_Assigned_to_you_with_the_first_issue_first()
    {
        var work = await _f.Shown(Token);

        Assert.Equal("Assigned to you", work.Groups[0].Heading);
        Assert.Equal(["ENG-12", "ENG-31", "OPS-40"], work.Mine.Rows.Select(r => r.Key), StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_groups_are_Assigned_to_you_then_Your_teams_and_each_is_read_with_50_to_a_page()
    {
        var work = await _f.Shown(Token);

        Assert.Equal(["Assigned to you", "Your teams"], work.Groups.Select(g => g.Heading), StringComparer.Ordinal);
        Assert.Equal(["ENG-57", "ENG-44"], work.Teams.Rows.Select(r => r.Key), StringComparer.Ordinal);
        Assert.Equal(("52", "3"), (work.Teams.Count, work.Mine.Count));
        Assert.Equal([("mine", 50), ("teams", 50)], _f.Server.LinearQueries.Select(q => (q.Group, q.Limit ?? 0)).Order());
    }

    [Fact]
    public async Task A_filter_change_sends_the_filter_with_both_groups_and_reads_again()
    {
        var work = await _f.Shown(Token);

        work.Filter("team").Pick("ENG");
        await work.Reading;
        work.Filter("priority").Pick("2");
        await work.Reading;

        var last = _f.Server.LinearQueries.TakeLast(2).ToList();
        Assert.Equal(["mine", "teams"], last.Select(q => q.Group).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.All(last, q => Assert.Equal(("ENG", 2), (q.Team, q.Priority)));
        Assert.True(work.HasFilters);
    }

    [Fact]
    public async Task The_filters_offer_the_choices_of_the_lookups_of_the_teams_in_the_rows()
    {
        var work = await _f.Shown(Token);

        Assert.Equal("ENG", Assert.Single(_f.Server.LinearLookupsRead, t => t == "ENG"));
        Assert.Contains("Todo", work.Filter("state").Choices.Select(c => c.Text), StringComparer.Ordinal);
        Assert.Contains("Sam Lee", work.Filter("assignee").Choices.Select(c => c.Text), StringComparer.Ordinal);
        Assert.DoesNotContain("Former Member", work.Filter("assignee").Choices.Select(c => c.Text), StringComparer.Ordinal);
        Assert.Contains("bug", work.Filter("label").Choices.Select(c => c.Text), StringComparer.Ordinal);
        Assert.Contains("Cycle 14", work.Filter("cycle").Choices.Select(c => c.Text), StringComparer.Ordinal);
        Assert.Contains("Sync worker", work.Filter("project").Choices.Select(c => c.Text), StringComparer.Ordinal);
        Assert.Contains("ENG", work.Filter("team").Choices.Select(c => c.Text), StringComparer.Ordinal);
        Assert.Equal(["Any priority", "Urgent", "High", "Medium", "Low", "No priority"], work.Filter("priority").Choices.Select(c => c.Text), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Clear_filters_turns_every_filter_off_and_reads_without_them()
    {
        var work = await _f.Shown(Token);
        work.Filter("team").Pick("ENG");
        await work.Reading;
        work.Filter("label").Pick("00000000-0000-4000-8000-0000000000d1");
        await work.Reading;

        work.ClearFilters();
        await work.Reading;

        Assert.False(work.HasFilters);
        Assert.All(_f.Server.LinearQueries.TakeLast(2), q => Assert.Equal((null, null), (q.Team, q.Label)));
    }

    [Fact]
    public async Task Show_more_reads_the_next_page_with_after_appends_it_and_moves_focus_to_its_first_row()
    {
        var first = LinearFixture.Golden("mine");
        first["next"] = "cursor-1";
        first["total"] = 60;
        var second = LinearFixture.Golden("mine");
        var row = second["rows"]![0]!.DeepClone();
        row["key"] = "ENG-77";
        row["link"] = null;
        row["pr"] = null;
        second["rows"] = new JsonArray(row);
        _f.Server.LinearIssuesFor = q => string.Equals(q.Group, "teams", StringComparison.Ordinal) ? _f.Teams : q.After is null ? LinearFixture.Served(first) : LinearFixture.Served(second);
        var work = await _f.Shown(Token);
        var focus = new List<FocusRequestedEventArgs>();
        work.FocusRequested += (_, e) => focus.Add(e);

        Assert.True(work.HasMore);
        await work.ShowMore();

        Assert.Equal("cursor-1", _f.Server.LinearQueries.Last(q => q.Group == "mine").After);
        Assert.Equal(["ENG-12", "ENG-31", "OPS-40", "ENG-77"], work.Mine.Rows.Select(r => r.Key), StringComparer.Ordinal);
        Assert.Equal(("ENG-77", false), (Assert.Single(focus).ItemId, focus[0].OnlyIfLost));
        Assert.False(work.HasMore);
    }

    [Fact]
    public async Task A_row_names_the_key_title_state_assignee_priority_and_the_run_state()
    {
        var work = await _f.Shown(Token);

        var row = work.Mine.Rows[0];
        Assert.Equal("ENG-12, Retry budget for the sync worker, In Progress, Joi Example, Urgent, running", row.Name);
        Assert.Equal(("Urgent", "PR #2071 waiting on you", true), (row.PriorityText, row.PrText, row.HasPr));
        Assert.Equal("ENG-31, Export limits: cap the CSV at 50k rows, Todo, Joi Example, High", work.Mine.Rows[1].Name);
        Assert.False(work.Mine.Rows[1].HasPr);
    }

    [Fact]
    public async Task An_unassigned_issue_without_a_priority_says_so_in_words()
    {
        var work = await _f.Shown(Token);

        var row = work.Teams.Rows[1];
        Assert.Equal(("Unassigned", "No priority"), (row.AssigneeName, row.PriorityText));
        Assert.Contains("Unassigned, No priority", row.Name, StringComparison.Ordinal);
        Assert.Equal("PR #2080 approved", work.Teams.Rows[0].PrText);
    }

    [Fact]
    public async Task A_row_asks_for_its_issue()
    {
        var work = await _f.Shown(Token);
        var asked = new List<string>();
        work.IssueRequested += (_, e) => asked.Add(e.Key);

        work.RequestIssue("ENG-12");

        Assert.Equal(["ENG-12"], asked);
    }
}
