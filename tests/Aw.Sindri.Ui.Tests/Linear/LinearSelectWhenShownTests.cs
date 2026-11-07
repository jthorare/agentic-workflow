using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Queue;

namespace Aw.Sindri.Ui.Tests.Linear;

/// <summary>A created issue's row taking selection and focus once the list shows it, and the wait ending when the list never does.</summary>
[Lane(TestLane.Unit)]
public sealed class LinearSelectWhenShownTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // The "mine" golden with one more row, as the list reads after an issue was created.
    private static RouteAnswer WithMineRow(string key)
    {
        var body = JsonNode.Parse(GoldenFiles.Api("linear-issues", "mine"))!.AsObject();
        var rows = body["rows"]!.AsArray();
        var added = rows[0]!.DeepClone();
        added["key"] = key;
        rows.Insert(0, added);
        return new RouteAnswer(HttpStatusCode.OK, body.ToJsonString());
    }

    [Fact]
    public Task A_created_issue_the_filters_hide_leaves_nothing_pending_and_asks_for_the_fallback_focus() => UiThread.Run(async () =>
    {
        var f = new LinearFixture();
        var work = await f.Shown(Token);
        var (missed, focused) = (0, new List<string>());
        work.SelectMissed += (_, _) => missed++;
        work.FocusRequested += (_, e) => focused.Add(e.ItemId);

        work.SelectWhenShown("ENG-99");
        await work.Reading;
        f.Mine = WithMineRow("ENG-99");   // a later read that does bring it
        await work.Refresh(LinearFixture.Url, Token);

        Assert.Equal((1, 0, null), (missed, focused.Count, work.SelectedKey));
    }, Token);

    [Fact]
    public Task A_filter_change_ends_the_wait_for_a_created_issue() => UiThread.Run(async () =>
    {
        var f = new LinearFixture();
        var work = await f.Shown(Token);
        var focused = new List<string>();
        work.FocusRequested += (_, e) => focused.Add(e.ItemId);
        f.Server.LinearIssuesFor = _ => RouteAnswer.Api("linear-issues", "mine");
        work.SelectWhenShown("ENG-99");   // the read is not awaited: the person picks a filter first
        work.Filter("team").Pick("ENG");
        f.Mine = WithMineRow("ENG-99");
        f.Server.LinearIssuesFor = query => string.Equals(query.Group, "teams", StringComparison.Ordinal) ? f.Teams : f.Mine;

        await work.Refresh(LinearFixture.Url, Token);

        Assert.Empty(focused);
    }, Token);

    [Fact]
    public Task A_created_issue_selected_after_the_dialog_closed_asks_for_focus_only_if_it_is_lost() => UiThread.Run(async () =>
    {
        var f = new LinearFixture();
        var work = await f.Shown(Token);
        f.Mine = WithMineRow("ENG-99");
        FocusRequestedEventArgs? request = null;
        work.FocusRequested += (_, e) => request = e;

        work.SelectWhenShown("ENG-99", takeFocus: false);
        await work.Reading;

        Assert.Equal(("ENG-99", true, "ENG-99"), (request?.ItemId, request?.OnlyIfLost, work.SelectedKey));
    }, Token);

    [Fact]
    public Task A_read_that_replaces_the_waiting_one_without_the_row_ends_the_wait_and_later_reads_move_no_focus() => UiThread.Run(async () =>
    {
        var f = new LinearFixture();
        var work = await f.Shown(Token);
        var (missed, focused) = (0, new List<string>());
        work.SelectMissed += (_, _) => missed++;
        work.FocusRequested += (_, e) => focused.Add(e.ItemId);

        var gate = f.Server.LinearIssuesGate = new TaskCompletionSource();
        work.SelectWhenShown("ENG-99");
        var waiting = work.Reading;
        var onItsWay = !waiting.IsCompleted;
        f.Server.LinearIssuesGate = null;
        await work.Refresh(LinearFixture.Url, Token);   // replaces the held read, without the row
        gate.SetResult();
        await waiting;
        f.Mine = WithMineRow("ENG-99");
        await work.Refresh(LinearFixture.Url, Token);

        Assert.Equal((true, 1, 0), (onItsWay, missed, focused.Count));
    }, Token);

    [Fact]
    public Task Leaving_Work_while_the_read_is_on_its_way_leaves_nothing_pending() => UiThread.Run(async () =>
    {
        var f = new LinearFixture();
        var work = await f.Shown(Token);
        var (missed, focused) = (0, new List<string>());
        work.SelectMissed += (_, _) => missed++;
        work.FocusRequested += (_, e) => focused.Add(e.ItemId);

        var gate = f.Server.LinearIssuesGate = new TaskCompletionSource();
        work.SelectWhenShown("ENG-99");
        var onItsWay = !work.Reading.IsCompleted;
        work.SetShown(shown: false);
        f.Server.LinearIssuesGate = null;
        gate.SetResult();
        await work.Reading;
        work.SetShown(shown: true);
        f.Mine = WithMineRow("ENG-99");
        await work.Refresh(LinearFixture.Url, Token);

        Assert.Equal((true, 0, 0), (onItsWay, missed, focused.Count));
    }, Token);
}
