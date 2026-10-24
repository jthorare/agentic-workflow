using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Item;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Item;

/// <summary>
/// The item detail sheet (UI spec §4.6, §4.9 Item detail) over GET /sindri/items/{id} and its timeline, with Close item
/// (CloseItem:done / :cancelled, desk, E-8) sending the version the human saw (§4.5, §9.1).
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ItemSheetViewModelTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static void IsObservable(object list) => _ = Assert.IsType<System.Collections.Specialized.INotifyCollectionChanged>(list, exactMatch: false);

    private async Task<ItemSheetViewModel> Sheet()
    {
        var catalog = new CatalogViewModel(new FakeClipboard(), _clock);
        catalog.Show(_api, _server, (await _server.Commands(_api, Token)).Value);
        return new ItemSheetViewModel(_server, catalog, _clock);
    }

    [Fact]
    public async Task Opening_reads_the_item_and_the_first_page_of_its_timeline()
    {
        var sheet = await Sheet();

        await sheet.Open(_api, A.NewItem, Token);

        Assert.Equal((A.NewItem, "Try the queue", "agentic-workflow", "New", "1", "5m"), (sheet.ItemId, sheet.Title, sheet.Repo, sheet.StateWord, sheet.VersionText, sheet.Age));
        Assert.Equal(("human:cli", false, false), (sheet.CreatedBy, sheet.HasWaitingOn, sheet.IsNotFound));
        Assert.Equal(["item.created", "item.linked"], sheet.Timeline.Take(2).Select(e => e.Type), StringComparer.Ordinal);
        Assert.Equal(100, sheet.Timeline.Count);
        Assert.Equal(("11:55:00", "human:cli", "Created “Try the queue” in agentic-workflow"), (sheet.Timeline[0].Time, sheet.Timeline[0].Actor, sheet.Timeline[0].Line));
        Assert.True(sheet.HasMore);
    }

    // F3/F4 (owner example): the sheet's sidebar leads with the title and one status sentence; who made it, the version and the
    // run's address wait behind Details, closed until asked for.
    [Fact]
    public async Task The_sheet_leads_with_a_status_sentence_and_keeps_the_bookkeeping_in_closed_details()
    {
        var sheet = await Sheet();

        await sheet.Open(_api, A.NewItem, Token);

        Assert.Equal(("New for 5m", false), (sheet.StatusLine, sheet.DetailsOpen));
    }

    // Review 456-3: the queue no longer draws the raw id; the sheet's Details carries it with Copy.
    [Fact]
    public async Task Copy_id_puts_the_items_id_on_the_clipboard()
    {
        var clipboard = new FakeClipboard();
        var catalog = new CatalogViewModel(clipboard, _clock);
        catalog.Show(_api, _server, (await _server.Commands(_api, Token)).Value);
        var sheet = new ItemSheetViewModel(_server, catalog, _clock);
        await sheet.Open(_api, A.NewItem, Token);

        await sheet.CopyId();

        Assert.Equal(A.NewItem, clipboard.Text);
    }

    // L1: the timeline is patched by sequence, so a refresh keeps the rows (and the focus on one) as they were.
    [Fact]
    public async Task A_refresh_keeps_the_timeline_rows_it_already_shows()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        IsObservable(sheet.Timeline);
        var second = sheet.Timeline[1];

        await sheet.Refresh(_api, Token);

        Assert.Same(second, sheet.Timeline[1]);
        Assert.Equal(100, sheet.Timeline.Count);
    }

    // §4.6 (owner, 2026-10-12): the sheet shows the item's body and link as GET /items/{itemId} serves them.
    [Fact]
    public async Task The_sheet_shows_the_items_body_and_link()
    {
        var sheet = await Sheet();

        await sheet.Open(_api, A.NewItem, Token);
        Assert.Equal((true, "Check the queue shows a new item without a click.", true, "linear:issue/ENG-142"), (sheet.HasBody, sheet.Body, sheet.HasLink, sheet.Link));

        await sheet.Open(_api, A.DecisionItem, Token);
        Assert.Equal((false, false), (sheet.HasBody, sheet.HasLink));
    }

    // README X61 (multiplayer later): createdBy and each entry's actor are opaque strings, shown exactly as served.
    [Fact]
    public async Task Actors_are_shown_exactly_as_served()
    {
        _server.Items[A.NewItem] = new RouteAnswer(System.Net.HttpStatusCode.OK, GoldenFiles.SpecShaped("items-id", "new").Replace("\"human:cli\"", "\"human:ada\"", StringComparison.Ordinal));
        _server.TimelinePages[0] = new RouteAnswer(System.Net.HttpStatusCode.OK, GoldenFiles.SpecShaped("items-id-timeline", "fixture").Replace("\"human:cli\"", "\"human:ada@laptop\"", StringComparison.Ordinal));
        var sheet = await Sheet();

        await sheet.Open(_api, A.NewItem, Token);

        Assert.Equal(("human:ada", "human:ada@laptop"), (sheet.CreatedBy, sheet.Timeline[0].Actor));
    }

    // §4.6: the timeline is paged by 'next'; Load more appends the next page and goes once there is none.
    [Fact]
    public async Task Load_more_appends_the_next_page_until_there_is_none()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);

        _ = await sheet.LoadMore(Token);

        Assert.Equal((101, 152L), (sheet.Timeline.Count, sheet.Timeline[^1].Sequence));
        Assert.False(sheet.HasMore);
    }

    // §7, §8: the sheet's actions are the item's 'actions' that the catalog has, in the API's order, named with the item.
    [Fact]
    public async Task The_actions_are_the_items_actions_rendered_from_the_catalog()
    {
        var sheet = await Sheet();

        await sheet.Open(_api, A.NewItem, Token);

        Assert.Equal(["Close item", "Close as cancelled"], sheet.Actions.Select(a => a.Label), StringComparer.Ordinal);
        Assert.All(sheet.Actions, a => Assert.True(a.CanRun));
        Assert.Equal((false, true), (sheet.Actions[0].IsDestructive, sheet.Actions[1].IsDestructive));
        Assert.Equal("Close item " + A.NewItem, sheet.Actions[0].Name);
    }

    // §4.5: Close item sends the version the human saw; the sheet then follows the views (no folding of the answer).
    [Fact]
    public async Task Close_done_sends_the_version_shown_and_a_refresh_shows_the_new_state()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        var close = sheet.Actions[0];

        await close.Press(Token);
        _server.Items[A.NewItem] = RouteAnswer.SpecShaped("items-id", "after-close");
        await sheet.Refresh(_api, Token);

        Assert.Equal([(A.NewItem, new Ui.Api.CloseItemRequest("done", 1))], _server.Closed);
        Assert.Equal(("Done", "2", 1), (sheet.StateWord, sheet.VersionText, sheet.Actions.Count));   // L8: the control shows its outcome first
        _clock.Advance(ControlViewModel.AcceptedFor);
        Assert.Empty(sheet.Actions);
    }

    // §9.1, §18 U2's signal: a stale version is SND-ITEM-409; the sheet shows its detail as served and re-reads the item.
    [Fact]
    public async Task A_stale_close_shows_SND_ITEM_409s_detail_and_rereads_the_item()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        _server.Close = RouteAnswer.Problem("items-id-close", "stale-version");
        _server.Items[A.NewItem] = RouteAnswer.SpecShaped("items-id", "after-close");   // closed elsewhere since
        var cancel = sheet.Actions[1];

        await cancel.Press(Token);
        Assert.True(cancel.IsConfirming);
        await cancel.Confirm(Token);

        Assert.Equal([(A.NewItem, new Ui.Api.CloseItemRequest("cancelled", 1))], _server.Closed);
        Assert.Equal(("SND-ITEM-409", A.Detail("items-id-close", "stale-version"), true), (sheet.ProblemCode, sheet.ProblemDetail, sheet.HasProblem));
        Assert.Equal(("Done", "2"), (sheet.StateWord, sheet.VersionText));
        Assert.Equal(2, _server.ItemCalls);
    }

    // §4.9 Item detail: an unknown item is SND-ITEM-404's detail with Back.
    [Fact]
    public async Task An_unknown_item_shows_SND_ITEM_404s_detail()
    {
        var sheet = await Sheet();

        await sheet.Open(_api, A.MissingItem, Token);

        Assert.Equal((true, "SND-ITEM-404", A.Detail("items-id", "not-found")), (sheet.IsNotFound, sheet.ProblemCode, sheet.ProblemDetail));
        Assert.Empty(sheet.Actions);
    }

    // §4.1: a refresh keeps each control (and its lifecycle) while the item still allows its action.
    [Fact]
    public async Task A_refresh_keeps_the_same_controls()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        var before = sheet.Actions.ToList();

        await sheet.Refresh(_api, Token);

        Assert.Equal(before, sheet.Actions);
        Assert.Equal(2, _server.TimelineCalls);
    }

    // The needs-decision item says what it waits on.
    [Fact]
    public async Task An_item_waiting_on_a_decision_names_it()
    {
        var sheet = await Sheet();

        await sheet.Open(_api, A.DecisionItem, Token);

        Assert.Equal((true, "DEC-0192a1b2c3d4e5f6a7b8c9d0e1f2c501", "Needs decision"), (sheet.HasWaitingOn, sheet.WaitingOn, sheet.StateWord));
        Assert.Equal(["Decide", "Close item", "Close as cancelled"], sheet.Actions.Select(a => a.Label), StringComparer.Ordinal);
    }

    private static Task Within(Task task) => task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, Token);

    private static TaskCompletionSource Gate() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    // L6: an answer for an item the sheet has moved on from is dropped, whatever order the reads finish in. With a wire that
    // ignores cancellation (a response already on its way) it is ReadItem's own generation check that drops it.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_held_refresh_of_one_item_never_overwrites_the_item_opened_since(bool wireIgnoresCancellation)
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        _server.IgnoreCancellation = wireIgnoresCancellation;
        var gate = _server.ItemGates[A.NewItem] = Gate();
        try
        {
            var held = sheet.Refresh(_api, Token);

            await Within(sheet.Open(_api, A.DecisionItem, Token));
            gate.SetResult();
            await Within(held);
        }
        finally
        {
            _ = gate.TrySetResult();
        }

        Assert.Equal((A.DecisionItem, "Needs decision", 3), (sheet.ItemId, sheet.StateWord, sheet.Actions.Count));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_held_first_read_of_one_item_is_dropped_when_another_is_opened(bool wireIgnoresCancellation)
    {
        var sheet = await Sheet();
        _server.IgnoreCancellation = wireIgnoresCancellation;
        var gate = _server.ItemGates[A.NewItem] = Gate();
        try
        {
            var held = sheet.Open(_api, A.NewItem, Token);

            await Within(sheet.Open(_api, A.DecisionItem, Token));
            gate.SetResult();
            await Within(held);
        }
        finally
        {
            _ = gate.TrySetResult();
        }

        Assert.Equal((A.DecisionItem, "Needs decision", false), (sheet.ItemId, sheet.StateWord, sheet.IsLoading));
    }

    // L6 (testing-1): the timeline page of an item the sheet has left, answered late by a wire that ignores cancellation, is
    // dropped by ReadTimeline's generation check; the other item's own timeline stays.
    [Fact]
    public async Task A_late_timeline_page_of_one_item_never_replaces_the_timeline_of_the_item_opened_since()
    {
        var sheet = await Sheet();
        _server.IgnoreCancellation = true;
        _server.ItemTimelinePages[(A.DecisionItem, 0)] = RouteAnswer.SpecShaped("items-id-timeline", "page-2");
        var gate = _server.TimelineGates[(A.NewItem, 0)] = Gate();
        try
        {
            var held = sheet.Open(_api, A.NewItem, Token);

            await Within(sheet.Open(_api, A.DecisionItem, Token));
            gate.SetResult();
            await Within(held);
        }
        finally
        {
            _ = gate.TrySetResult();
        }

        Assert.Equal((A.DecisionItem, 1, false), (sheet.ItemId, sheet.Timeline.Count, sheet.HasMore));
        Assert.Equal(152, sheet.Timeline[0].Sequence);
    }

    // L6: Load more while a page is in flight does nothing (the button stays focusable, so a second press is possible).
    [Fact]
    public async Task A_second_load_more_while_one_is_in_flight_reads_nothing()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        var gate = _server.TimelineGates[(A.NewItem, 151)] = Gate();
        var calls = _server.TimelineCalls;
        try
        {
            var first = sheet.LoadMore(Token);

            var second = await WithinResult(sheet.LoadMore(Token));
            gate.SetResult();

            Assert.Equal((true, false), (await WithinResult(first), second));
        }
        finally
        {
            _ = gate.TrySetResult();
        }

        Assert.Equal(calls + 1, _server.TimelineCalls);
        Assert.Equal(101, sheet.Timeline.Count);
    }

    private static Task<T> WithinResult<T>(Task<T> task) => task.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, Token);

    // corr-2: a Load more still in flight for the item the sheet has left must not stop Load more on the item now open; and
    // when the old one lands (first), its guarded finally must not clear the flag the new item's Load more set.
    [Fact]
    public async Task A_load_more_in_flight_for_the_previous_item_does_not_block_or_unblock_the_next_items()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        _server.IgnoreCancellation = true;
        var gateA = _server.TimelineGates[(A.NewItem, 151)] = Gate();
        var gateB = _server.TimelineGates[(A.DecisionItem, 151)] = Gate();
        bool oldOne, firstOnNew, secondOnNew;
        try
        {
            var stuck = sheet.LoadMore(Token);
            await Within(sheet.Open(_api, A.DecisionItem, Token));
            var onNew = sheet.LoadMore(Token);
            var calls = _server.TimelineCalls;

            gateA.SetResult();
            oldOne = await WithinResult(stuck);
            secondOnNew = await WithinResult(sheet.LoadMore(Token));   // the new item's is still in flight: refused, not sent twice
            Assert.Equal(calls, _server.TimelineCalls);

            gateB.SetResult();
            firstOnNew = await WithinResult(onNew);
        }
        finally
        {
            _ = gateA.TrySetResult();
            _ = gateB.TrySetResult();
        }

        Assert.Equal((false, true, false, 101), (oldOne, firstOnNew, secondOnNew, sheet.Timeline.Count));
    }

    // testing-2: a page that lands after the sheet moved to another item is not that item's: no row of the old item's page is
    // added, whether or not the two timelines point at the same next page.
    [Fact]
    public async Task A_load_more_that_lands_after_another_item_was_opened_adds_nothing_to_it()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        var gate = _server.TimelineGates[(A.NewItem, 151)] = Gate();
        bool appended;
        try
        {
            var more = sheet.LoadMore(Token);
            await Within(sheet.Open(_api, A.DecisionItem, Token));
            gate.SetResult();
            appended = await WithinResult(more);
        }
        finally
        {
            _ = gate.TrySetResult();
        }

        Assert.False(appended);
        Assert.Equal((A.DecisionItem, 100, true), (sheet.ItemId, sheet.Timeline.Count, sheet.HasMore));
        Assert.DoesNotContain(sheet.Timeline, r => r.Sequence == 152);
    }

    // testing-2: a refresh that moved the cursor while Load more was in flight makes that page the wrong one: it is dropped.
    [Fact]
    public async Task A_load_more_that_lands_after_a_refresh_moved_the_cursor_is_dropped()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        var gate = _server.TimelineGates[(A.NewItem, 151)] = Gate();
        bool appended;
        try
        {
            var more = sheet.LoadMore(Token);
            _server.TimelinePages[0] = new RouteAnswer(System.Net.HttpStatusCode.OK, GoldenFiles.SpecShaped("items-id-timeline", "fixture").Replace("\"next\": 151", "\"next\": 200", StringComparison.Ordinal));
            await Within(sheet.Refresh(_api, Token));
            gate.SetResult();
            appended = await WithinResult(more);
        }
        finally
        {
            _ = gate.TrySetResult();
        }

        Assert.False(appended);
        Assert.Equal((100, true), (sheet.Timeline.Count, sheet.HasMore));
    }

    // L7 (corr-7): a first read that fails with anything but 404 shows the API's code and detail; the next good read clears them.
    [Fact]
    public async Task A_failed_first_read_shows_the_apis_code_and_detail_until_a_read_succeeds()
    {
        _server.Items[A.NewItem] = RouteAnswer.Problem(System.Net.HttpStatusCode.ServiceUnavailable, "SND-API-011", "The views didn't catch up within 2 s (fix: retry)");
        var sheet = await Sheet();

        await sheet.Open(_api, A.NewItem, Token);

        Assert.Equal((false, false, true, "SND-API-011", "The views didn't catch up within 2 s (fix: retry)"), (sheet.IsLoading, sheet.HasItem, sheet.HasProblem, sheet.ProblemCode, sheet.ProblemDetail));

        _server.Items[A.NewItem] = RouteAnswer.SpecShaped("items-id", "new");
        await sheet.Refresh(_api, Token);

        Assert.Equal((true, false), (sheet.HasItem, sheet.HasProblem));
    }

    // L13 (testing-1): a read made while the views are behind the last command is SND-API-011, once. The sheet keeps its last
    // answer marked stale; the next read (the views caught up) shows the new state.
    [Fact]
    public async Task A_read_the_views_are_behind_for_keeps_the_last_answer_stale_until_they_catch_up()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        var before = (sheet.StateWord, sheet.VersionText, sheet.Timeline.Count);
        _server.Lag = ViewsLag.Behind;
        _ = await _server.CloseItem(_api, A.NewItem, new Ui.Api.CloseItemRequest("done", 1), Token);
        _server.Items[A.NewItem] = RouteAnswer.SpecShaped("items-id", "after-close");

        await sheet.Refresh(_api, Token);

        Assert.Equal((true, true, before), (sheet.IsStale, sheet.HasItem, (sheet.StateWord, sheet.VersionText, sheet.Timeline.Count)));

        await sheet.Refresh(_api, Token);

        Assert.Equal((false, "Done", "2"), (sheet.IsStale, sheet.StateWord, sheet.VersionText));
    }

    // L15 (testing-2): a refresh re-reads every page Load more showed, from the first, so it never shrinks the timeline.
    [Fact]
    public async Task A_refresh_after_load_more_keeps_all_the_pages()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        _ = await sheet.LoadMore(Token);
        var calls = _server.TimelineCalls;

        await sheet.Refresh(_api, Token);

        Assert.Equal(calls + 2, _server.TimelineCalls);   // both pages, the first from the start
        Assert.Equal((101, 152L, false), (sheet.Timeline.Count, sheet.Timeline[^1].Sequence, sheet.HasMore));
    }

    // L15: a page that fails part-way through a re-read leaves the timeline as it was, in full, rather than a shorter one.
    [Fact]
    public async Task A_page_error_part_way_through_a_re_read_keeps_the_old_timeline()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        _ = await sheet.LoadMore(Token);
        var last = sheet.Timeline[^1];
        _server.TimelinePages[151] = RouteAnswer.Problem(System.Net.HttpStatusCode.ServiceUnavailable, "SND-API-011", "The views didn't catch up within 2 s (fix: retry)");

        await sheet.Refresh(_api, Token);

        Assert.Equal((101, false), (sheet.Timeline.Count, sheet.HasMore));
        Assert.Same(last, sheet.Timeline[^1]);
    }

    // L15: a Load more that fails shows nothing new and keeps its place: the same press, once the page answers, adds it.
    [Fact]
    public async Task A_failed_load_more_leaves_the_rows_and_the_cursor_as_they_were()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        var good = _server.TimelinePages[151];
        _server.TimelinePages[151] = RouteAnswer.Problem(System.Net.HttpStatusCode.ServiceUnavailable, "SND-API-011", "The views didn't catch up within 2 s (fix: retry)");

        var failed = await sheet.LoadMore(Token);

        Assert.Equal((false, 100, true), (failed, sheet.Timeline.Count, sheet.HasMore));
        Assert.Equal((true, "SND-API-011: The views didn't catch up within 2 s (fix: retry)"), (sheet.IsAnnouncing, sheet.Announcement));

        _server.TimelinePages[151] = good;
        var landed = await sheet.LoadMore(Token);
        Assert.True(landed);

        Assert.Equal((101, 152L, false), (sheet.Timeline.Count, sheet.Timeline[^1].Sequence, sheet.HasMore));
    }

    // U3' T8: the item's decisions and requests, read with the item.
    private const string DecisionItem = "ITM-0000000000000000000000000000000e";

    private async Task<ItemSheetViewModel> SheetWithNeeds()
    {
        _server.DecisionsByItem[DecisionItem] = RouteAnswer.Api("decisions", "by-item");
        _server.RequestsByItem[DecisionItem] = RouteAnswer.Api("requests", "list-by-item");
        _server.Items[DecisionItem] = _server.Items[A.NewItem];
        var sheet = await Sheet();
        await sheet.Open(_api, DecisionItem, Token);
        return sheet;
    }

    [Fact]
    public async Task Lists_pending_decisions_for_the_item_with_option_buttons()
    {
        var sheet = await SheetWithNeeds();
        var served = GoldenFiles.Node("decisions", "by-item")["decisions"]![0]!;

        var decision = Assert.Single(sheet.Needs.Decisions);

        Assert.Equal(((string)served["id"]!, (string)served["question"]!, (string)served["filedBy"]!), (decision.Id, decision.Question, decision.FiledBy));
        Assert.Equal(served["options"]!.AsArray().Select(o => (string)o!["label"]!), decision.Options.Select(o => o.Label), StringComparer.Ordinal);
        Assert.Equal([true, false], decision.Options.Select(o => o.IsRecommended));
        Assert.Contains(_server.DecisionQueries, q => q.Pending == true && string.Equals(q.Item, DecisionItem, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Lists_requests_filed_against_the_item()
    {
        var sheet = await SheetWithNeeds();
        var served = GoldenFiles.Node("requests", "list-by-item")["requests"]![0]!;

        var request = Assert.Single(sheet.Needs.Requests);

        Assert.Equal(((string)served["id"]!, (string)served["kind"]!, (string)served["text"]!, (string)served["actor"]!, (string)served["state"]!),
            (request.Id, request.Kind, request.Text, request.Actor, request.State));
        Assert.Contains(_server.RequestQueries, q => q.Item == DecisionItem);
    }

    [Fact]
    public async Task Shows_the_served_empty_sentences_for_the_item()
    {
        _server.DecisionsByItem[DecisionItem] = RouteAnswer.Api("decisions", "empty");
        _server.Items[DecisionItem] = _server.Items[A.NewItem];
        var sheet = await Sheet();

        await sheet.Open(_api, DecisionItem, Token);

        Assert.Empty(sheet.Needs.Decisions);
        Assert.Empty(sheet.Needs.Requests);
        Assert.Equal((GoldenFiles.Node("decisions", "empty")["empty"]!.ToString(), GoldenFiles.Node("requests", "list-empty")["empty"]!.ToString()), (sheet.Needs.DecisionsEmpty, sheet.Needs.RequestsEmpty));
    }

    [Fact]
    public async Task An_option_press_decides_once_and_rereads_the_lists()
    {
        var sheet = await SheetWithNeeds();
        var decision = sheet.Needs.Decisions[0];

        await sheet.Needs.Choose(decision.Id, decision.Options[0].Id);
        await sheet.Needs.Choose(decision.Id, decision.Options[0].Id);

        Assert.Equal([(decision.Id, decision.Options[0].Id)], [.. _server.DecideCalls.Select(c => (c.Decision, c.Request.Choice))]);
        Assert.All(sheet.Needs.Decisions[0].Options, o => Assert.False(o.IsEnabled));
    }

    [Fact]
    public async Task A_refused_Decide_shows_the_served_detail_inline()
    {
        var sheet = await SheetWithNeeds();
        _server.Decided = RouteAnswer.Problem("decisions-id", "decided-elsewhere");

        await sheet.Needs.Choose(sheet.Needs.Decisions[0].Id, "accept");

        Assert.Contains((string)GoldenFiles.Node("decisions-id", "decided-elsewhere")["detail"]!, sheet.Needs.Refusal, StringComparison.Ordinal);
    }
}
