using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Linear;

/// <summary>What a refresh keeps (Linear hub spec §3): the pages "Show more" loaded, the lookups a stale read asked for, a Dismiss that a held read must not undo, and the order of the filter choices.</summary>
[Lane(TestLane.Unit)]
public sealed class LinearWorkRefreshTests
{
    private readonly LinearFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static JsonNode Page(int from, int count, string? next, int total)
    {
        var page = LinearFixture.Golden("mine");
        var rows = new JsonArray();
        for (var i = 0; i < count; i++)
        {
            var row = page["rows"]![0]!.DeepClone();
            row["key"] = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"ENG-{from + i}");
            row["link"] = null;
            row["pr"] = null;
            rows.Add(row);
        }

        page["rows"] = rows;
        page["next"] = next;
        page["total"] = total;
        return page;
    }

    [Fact]
    public async Task An_event_refresh_keeps_the_pages_that_Show_more_loaded()
    {
        var first = Page(from: 100, count: 50, next: "c1", total: 52);
        var second = Page(from: 200, count: 2, next: null, total: 52);
        var merged = Page(from: 100, count: 50, next: null, total: 52);
        foreach (var row in second["rows"]!.AsArray())
        {
            merged["rows"]!.AsArray().Add(row!.DeepClone());
        }

        _f.Server.LinearIssuesFor = q =>
            string.Equals(q.Group, "teams", StringComparison.Ordinal) ? _f.Teams
            : string.Equals(q.After, "c1", StringComparison.Ordinal) ? LinearFixture.Served(second)
            : q.Limit > 50 ? LinearFixture.Served(merged) : LinearFixture.Served(first);
        var work = await _f.Shown(Token);
        await work.ShowMore();
        Assert.Equal(52, work.Mine.Rows.Count);

        await work.Refresh(LinearFixture.Url, Token);

        Assert.Equal(52, _f.Server.LinearQueries.Last(q => string.Equals(q.Group, "mine", StringComparison.Ordinal)).Limit);
        Assert.Equal((52, "ENG-201", false), (work.Mine.Rows.Count, work.Mine.Rows[^1].Key, work.HasMore));
    }

    [Fact]
    public async Task A_filter_change_starts_again_from_the_first_page()
    {
        var work = await _f.Shown(Token);

        work.Filter("team").Pick("ENG");
        await work.Reading;

        Assert.All(_f.Server.LinearQueries.TakeLast(2), q => Assert.Equal(50, q.Limit));
    }

    [Fact]
    public async Task Lookups_asked_by_a_read_that_a_newer_one_replaced_still_fill_the_filters()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _f.Server.LinearLookupsGate = gate;
        var work = _f.Work();
        work.Choose(linear: true);
        work.SetShown(shown: true);
        var older = work.Refresh(LinearFixture.Url, Token);

        work.Filter("team").Pick("ENG");
        gate.SetResult();
        await older;
        await work.Reading;

        Assert.Contains("Sam Lee", work.Filter("assignee").Choices.Select(c => c.Text), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_team_whose_lookups_failed_is_asked_again_by_the_next_read()
    {
        _f.Server.LinearLookupsAnswer = RouteAnswer.Refused();
        var work = await _f.Shown(Token);

        await work.Refresh(LinearFixture.Url, Token);

        Assert.Equal(2, _f.Server.LinearLookupsRead.Count(t => string.Equals(t, "ENG", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_team_whose_lookups_worked_is_not_asked_again()
    {
        var work = await _f.Shown(Token);

        await work.Refresh(LinearFixture.Url, Token);

        Assert.Equal(1, _f.Server.LinearLookupsRead.Count(t => string.Equals(t, "ENG", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_since_read_still_on_its_way_does_not_show_dismissed_rows_again()
    {
        var work = await _f.Shown(Token);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _f.Server.LinearSinceGate = gate;
        var held = work.Refresh(LinearFixture.Url, Token);

        await work.Dismiss();
        gate.SetResult();
        await held;

        Assert.Equal((false, 0, 0), (work.ShowsStrip, work.NewCount, work.StripRows.Count));
    }

    [Fact]
    public async Task The_choices_are_sorted_names_A_to_Z_priorities_in_order_and_states_by_the_boards_position()
    {
        var work = await _f.Shown(Token);

        Assert.Equal(["Any assignee", "Joi Example", "Sam Lee"], work.Filter("assignee").Choices.Select(c => c.Text), StringComparer.Ordinal);
        Assert.Equal(["Any priority", "Urgent", "High", "Medium", "Low", "No priority"], work.Filter("priority").Choices.Select(c => c.Text), StringComparer.Ordinal);
        Assert.Equal(["Open", "Backlog", "Todo", "In Progress", "In Review", "Done"], work.Filter("state").Choices.Select(c => c.Text).Take(6), StringComparer.Ordinal);
        Assert.Equal(["Any label", "api", "bug"], work.Filter("label").Choices.Select(c => c.Text), StringComparer.Ordinal);
    }

    // KeepSelection: a read that no longer serves the selected issue clears the selection, so Enter can't open an issue that isn't shown.
    [Fact]
    public async Task A_read_that_drops_the_selected_issue_clears_the_selection()
    {
        var work = await _f.Shown(Token);
        work.Move(1);
        Assert.Equal("ENG-12", work.SelectedKey);

        var without = LinearFixture.Golden("mine");
        var kept = new JsonArray([.. without["rows"]!.AsArray().Where(r => !string.Equals((string?)r!["key"], "ENG-12", StringComparison.Ordinal)).Select(r => r!.DeepClone())]);
        without["rows"] = kept;
        _f.Server.LinearIssuesFor = q => string.Equals(q.Group, "teams", StringComparison.Ordinal) ? _f.Teams : LinearFixture.Served(without);
        await work.Refresh(LinearFixture.Url, Token);
        var asked = 0;
        work.IssueRequested += (_, _) => asked++;

        Assert.Equal((null, false, 0, false), (work.SelectedKey, work.OpenSelected(), asked, work.Mine.Rows.Concat(work.Teams.Rows).Any(r => r.IsSelected)));
        work.Move(1);
        Assert.Equal(work.Mine.Rows[0].Key, work.SelectedKey);
    }
}
