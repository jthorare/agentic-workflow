using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Item;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Item;

/// <summary>
/// The sheet's controls through a Close (UI spec §4.1, §9.1): a control in flight stays until it settles, the sheet speaks
/// the problem a gone control leaves behind once, SND-ITEM-409 where the control survives, and a question that outlives
/// the version it was asked about.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ItemSheetActionsTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static RouteAnswer NewAtVersion(int version) =>
        new(System.Net.HttpStatusCode.OK, GoldenFiles.SpecShaped("items-id", "new").Replace("\"version\": 1,", "\"version\": " + version.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",", StringComparison.Ordinal));

    private async Task<ItemSheetViewModel> Sheet()
    {
        var catalog = new CatalogViewModel(new FakeClipboard(), _clock);
        catalog.Show(_api, _server, (await _server.Commands(_api, Token)).Value);
        return new ItemSheetViewModel(_server, catalog, _clock);
    }

    private static List<string> Spoken(ItemSheetViewModel sheet)
    {
        var spoken = new List<string>();
        sheet.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ItemSheetViewModel.Announcement) && sheet.IsAnnouncing)
            {
                spoken.Add(sheet.Announcement);
            }
        };
        return spoken;
    }

    private static List<string> Spoken(ControlViewModel control)
    {
        var spoken = new List<string>();
        control.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ControlViewModel.Announcement) && control.IsAnnouncing)
            {
                spoken.Add(control.Announcement);
            }
        };
        return spoken;
    }

    // ux-A-3: Close item is one press away from ending the item, so it asks first like Close as cancelled: nothing is sent until Confirm.
    [Fact]
    public async Task Close_item_asks_before_it_sends_and_Confirm_sends_once()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        var close = sheet.Actions[0];

        await close.Press(Token);

        Assert.Equal(("Close item", true), (close.Label, close.IsConfirming));
        Assert.Empty(_server.Closed);

        await close.Confirm(Token);

        Assert.Equal([(A.NewItem, new Ui.Api.CloseItemRequest("done", 1))], _server.Closed);
    }

    [Fact]
    public async Task Keeping_Close_item_sends_nothing()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        var close = sheet.Actions[0];
        await close.Press(Token);

        close.Keep();

        Assert.Equal((false, 0), (close.IsConfirming, _server.Closed.Count));
    }

    // L8: the item moved on before the answer came back; the control that sent the command stays to show its outcome.
    [Fact]
    public async Task A_control_in_flight_stays_until_it_settles_even_when_the_item_no_longer_allows_it()
    {
        _server.OnClosed = (_, _) => _server.Items[A.NewItem] = RouteAnswer.SpecShaped("items-id", "after-close");
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        var close = sheet.Actions[0];
        var spoken = Spoken(close);

        await close.Press(Token);
        await close.Confirm(Token);
        await sheet.Refresh(_api, Token);

        Assert.Equal((CommandPhase.Accepted, "Done", true), (close.Phase, sheet.StateWord, sheet.Actions.Contains(close)));
        _clock.Advance(ControlViewModel.AcceptedFor);

        Assert.Equal((CommandPhase.Idle, 0), (close.Phase, sheet.Actions.Count));
        _ = Assert.Single(spoken, s => s.EndsWith("done", StringComparison.Ordinal));
    }

    // L8: the control that sent the refused command is gone with its action; the sheet says why, once, and the control stays quiet.
    [Fact]
    public async Task A_409_for_a_control_the_item_no_longer_allows_is_spoken_once_by_the_sheet()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        _server.Close = RouteAnswer.Problem("items-id-close", "stale-version");
        _server.Items[A.NewItem] = RouteAnswer.SpecShaped("items-id", "after-close");
        var cancel = sheet.Actions[1];
        var sheetSpoken = Spoken(sheet);
        var controlSpoken = Spoken(cancel);
        Assert.Equal((false, "Sindri"), (sheet.IsAnnouncing, sheet.Announcement));

        await cancel.Press(Token);
        await cancel.Confirm(Token);
        await sheet.Refresh(_api, Token);

        Assert.Equal(["SND-ITEM-409: " + A.Detail("items-id-close", "stale-version")], sheetSpoken);
        Assert.DoesNotContain(controlSpoken, s => s.StartsWith("SND-ITEM-409", StringComparison.Ordinal));
        Assert.Empty(sheet.Actions);
    }

    // L8: the 404 line is spoken once too, however many reads find the item still missing.
    [Fact]
    public async Task A_404_is_spoken_once_by_the_sheet()
    {
        var sheet = await Sheet();
        var spoken = Spoken(sheet);

        await sheet.Open(_api, A.MissingItem, Token);
        await sheet.Refresh(_api, Token);

        Assert.Equal(["SND-ITEM-404: " + A.Detail("items-id", "not-found")], spoken);
    }

    // L9 (testing-3): the control survives the re-read, so the refusal shows on it and not on the sheet; the retry sends the new version.
    [Fact]
    public async Task A_409_where_the_control_survives_shows_inline_and_the_retry_sends_the_new_version()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        _server.Close = RouteAnswer.Problem("items-id-close", "stale-version");
        _server.Items[A.NewItem] = NewAtVersion(2);
        var close = sheet.Actions[0];

        await close.Press(Token);
        await close.Confirm(Token);

        Assert.Equal((false, "SND-ITEM-409", A.Detail("items-id-close", "stale-version")), (sheet.HasProblem, close.ErrorCode, close.ErrorDetail));
        Assert.Equal("2", sheet.VersionText);
        Assert.Contains(close, sheet.Actions);

        _server.Close = RouteAnswer.SpecShaped("items-id-close", "accepted");
        await close.Press(Token);
        await close.Confirm(Token);

        Assert.Equal([(A.NewItem, new Ui.Api.CloseItemRequest("done", 1)), (A.NewItem, new Ui.Api.CloseItemRequest("done", 2))], _server.Closed);
    }

    // corr-9: the human answers the question they were asked; when the item moved to another version meanwhile, the question goes.
    [Fact]
    public async Task A_question_asked_at_one_version_is_cancelled_when_the_item_moves_to_another()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        var cancel = sheet.Actions[1];
        await cancel.Press(Token);
        Assert.True(cancel.IsConfirming);

        _server.Items[A.NewItem] = NewAtVersion(2);
        await sheet.Refresh(_api, Token);

        Assert.False(cancel.IsConfirming);
        Assert.Empty(_server.Closed);
    }

    // corr-9: a refresh that finds the same version leaves the question alone.
    [Fact]
    public async Task A_question_stays_while_the_version_does()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        var cancel = sheet.Actions[1];
        await cancel.Press(Token);

        await sheet.Refresh(_api, Token);

        Assert.True(cancel.IsConfirming);
    }

    private static async Task Until(Func<bool> condition)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(Token);
        limit.CancelAfter(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(5, limit.Token);
        }
    }

    // corr-0: the 409's re-read was superseded by a refresh that served the closed item; the refusal is not lost.
    [Fact]
    public Task A_409_whose_re_read_a_refresh_took_over_is_still_spoken_once_by_the_sheet() =>
        UiThread.Run(async () =>
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        _server.Close = RouteAnswer.Problem("items-id-close", "stale-version");
        _server.Items[A.NewItem] = RouteAnswer.SpecShaped("items-id", "after-close");
        var gate = _server.ItemGates[A.NewItem] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var spoken = Spoken(sheet);
        var calls = _server.ItemCalls;
        try
        {
            await sheet.Actions[0].Press(Token);
            var press = sheet.Actions[0].Confirm(Token);
            await Until(() => _server.ItemCalls == calls + 1);
            var refresh = sheet.Refresh(_api, Token);
            await Until(() => _server.ItemCalls == calls + 2);

            gate.SetResult();
            await Task.WhenAll(press, refresh).WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, Token);
        }
        finally
        {
            _ = gate.TrySetResult();
        }

        Assert.Equal("SND-ITEM-409", sheet.ProblemCode);
        Assert.Equal(["SND-ITEM-409: " + A.Detail("items-id-close", "stale-version")], spoken);
    }, Token);

    // corr-0 (run 3): the control and the sheet both have a live region; a 409 whose re-read a refresh took over is spoken by
    // exactly one of them, once.
    [Fact]
    public Task A_409_whose_re_read_a_refresh_took_over_is_spoken_once_across_the_control_and_the_sheet() =>
        UiThread.Run(async () =>
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        _server.Close = RouteAnswer.Problem("items-id-close", "stale-version");
        _server.Items[A.NewItem] = RouteAnswer.SpecShaped("items-id", "after-close");
        var gate = _server.ItemGates[A.NewItem] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var close = sheet.Actions[0];
        var spoken = Spoken(sheet);
        var controlSpoke = Spoken(close);
        var calls = _server.ItemCalls;
        try
        {
            await close.Press(Token);
            var press = close.Confirm(Token);
            await Until(() => _server.ItemCalls == calls + 1);
            var refresh = sheet.Refresh(_api, Token);
            await Until(() => _server.ItemCalls == calls + 2);

            gate.SetResult();
            await Task.WhenAll(press, refresh).WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, Token);
        }
        finally
        {
            _ = gate.TrySetResult();
        }

        var code = "SND-ITEM-409";
        Assert.Equal(1, spoken.Concat(controlSpoke).Count(t => t.Contains(code, StringComparison.Ordinal)));
    }, Token);

    // corr-1: the 409 belongs to the item it was sent for; the sheet has moved on, so item B shows nothing of it.
    [Fact]
    public async Task A_409_for_an_item_the_sheet_has_moved_on_from_is_not_shown_or_spoken_on_the_next_one()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        _server.Close = RouteAnswer.Problem("items-id-close", "stale-version");
        _server.Items[A.DecisionItem] = RouteAnswer.SpecShaped("items-id", "after-close");
        var gate = _server.CommandGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var spoken = Spoken(sheet);
        try
        {
            await sheet.Actions[0].Press(Token);
            var press = sheet.Actions[0].Confirm(Token);
            await Until(() => _server.Closed.Count == 1);
            await sheet.Open(_api, A.DecisionItem, Token);

            gate.SetResult();
            await press.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, Token);
        }
        finally
        {
            _ = gate.TrySetResult();
        }

        Assert.Equal((A.DecisionItem, false, 0), (sheet.ItemId, sheet.HasProblem, spoken.Count));
    }

    // corr-4: the 409 line is the state at the conflict; once the version moves past it, a good read clears the line.
    [Fact]
    public async Task The_409_line_goes_when_the_version_moves_past_the_conflict()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        _server.Close = RouteAnswer.Problem("items-id-close", "stale-version");
        _server.Items[A.NewItem] = RouteAnswer.SpecShaped("items-id", "after-close");
        await sheet.Actions[0].Press(Token);
        await sheet.Actions[0].Confirm(Token);
        Assert.True(sheet.HasProblem);

        _server.Items[A.NewItem] = NewAtVersion(5);
        await sheet.Refresh(_api, Token);

        Assert.Equal((false, 2), (sheet.HasProblem, sheet.Actions.Count));
    }

    // testing-2: the line the 409 leaves is cleared by either arm of ConflictPassed alone, and by neither when nothing changed.
    [Theory]
    [InlineData("same", true)]
    [InlineData("version", false)]
    [InlineData("action", false)]
    public async Task The_409_line_is_cleared_by_each_arm_of_ConflictPassed_and_kept_when_neither_holds(string arm, bool stays)
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        _server.Close = RouteAnswer.Problem("items-id-close", "stale-version");
        _server.Items[A.NewItem] = RouteAnswer.SpecShaped("items-id", "after-close");   // version 2, Close not allowed
        await sheet.Actions[0].Press(Token);
        await sheet.Actions[0].Confirm(Token);
        Assert.True(sheet.HasProblem);

        _server.Items[A.NewItem] = arm switch
        {
            "version" => new RouteAnswer(System.Net.HttpStatusCode.OK, GoldenFiles.SpecShaped("items-id", "after-close").Replace("\"version\": 2,", "\"version\": 3,", StringComparison.Ordinal)),   // the version moved, the action is still not allowed
            "action" => NewAtVersion(2),   // the same version, the action allowed again
            _ => RouteAnswer.SpecShaped("items-id", "after-close"),
        };
        await sheet.Refresh(_api, Token);

        Assert.Equal(stays, sheet.HasProblem);
    }

    // R4: the wait for a 409's re-read always ends. The sheet re-reads, a refresh takes the read over, and then ...
    private async Task<(ItemSheetViewModel Sheet, ControlViewModel Close, TaskCompletionSource Gate, Task Press, CancellationTokenSource Refreshing)> Waiting()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);
        _server.Close = RouteAnswer.Problem("items-id-close", "stale-version");
        _server.Items[A.NewItem] = RouteAnswer.SpecShaped("items-id", "after-close");
        var gate = _server.ItemGates[A.NewItem] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var close = sheet.Actions[0];
        var calls = _server.ItemCalls;
        await close.Press(Token);
        var press = close.Confirm(Token);
        await Until(() => _server.ItemCalls == calls + 1);
        var refreshing = CancellationTokenSource.CreateLinkedTokenSource(Token);
        return (sheet, close, gate, press, refreshing);
    }

    // (a) ... the refresh answers 404: the press completes, and the 404 is spoken once.
    [Fact]
    public Task A_409_whose_re_read_a_refresh_took_over_and_found_the_item_gone_ends_and_speaks_the_404_once() =>
        UiThread.Run(async () =>
    {
        var (sheet, close, gate, press, refreshing) = await Waiting();
        var spoken = Spoken(sheet);
        var controlSpoke = Spoken(close);
        try
        {
            _server.Items[A.NewItem] = RouteAnswer.Problem("items-id", "not-found");
            var calls = _server.ItemCalls;
            var refresh = sheet.Refresh(_api, refreshing.Token);
            await Until(() => _server.ItemCalls == calls + 1);

            gate.SetResult();
            await Task.WhenAll(press, refresh).WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, Token);
        }
        finally
        {
            _ = gate.TrySetResult();
            refreshing.Dispose();
        }

        Assert.Equal(1, spoken.Concat(controlSpoke).Count(t => t.Contains("SND-ITEM-404", StringComparison.Ordinal)));
    }, Token);

    // (b) ... the sheet moves to another item: the press completes and the other item shows nothing of the 409.
    [Fact]
    public Task A_409_whose_re_read_is_waiting_when_the_sheet_opens_another_item_ends_and_shows_nothing_there() =>
        UiThread.Run(async () =>
    {
        var (sheet, _, gate, press, refreshing) = await Waiting();
        _server.Items[A.DecisionItem] = RouteAnswer.SpecShaped("items-id", "after-close");
        var spoken = Spoken(sheet);
        try
        {
            await sheet.Open(_api, A.DecisionItem, Token);

            gate.SetResult();
            await press.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, Token);
        }
        finally
        {
            _ = gate.TrySetResult();
            refreshing.Dispose();
        }

        Assert.Equal((A.DecisionItem, false, 0), (sheet.ItemId, sheet.HasProblem, spoken.Count));
    }, Token);

    // (c) ... the refresh itself is cancelled by its caller: the press completes, and the control speaks the 409 itself, once.
    [Fact]
    public Task A_409_whose_superseding_refresh_is_cancelled_ends_and_the_control_speaks_it_once() =>
        UiThread.Run(async () =>
    {
        var (sheet, close, gate, press, refreshing) = await Waiting();
        var spoken = Spoken(sheet);
        var controlSpoke = Spoken(close);
        try
        {
            var calls = _server.ItemCalls;
            var refresh = sheet.Refresh(_api, refreshing.Token);
            await Until(() => _server.ItemCalls == calls + 1);
            await refreshing.CancelAsync();
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => refresh);

            await press.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, Token);
        }
        finally
        {
            _ = gate.TrySetResult();
            refreshing.Dispose();
        }

        var code = "SND-ITEM-409";
        Assert.Equal(1, spoken.Concat(controlSpoke).Count(t => t.Contains(code, StringComparison.Ordinal)));
    }, Token);

    // R7: the sheet's action set has one source: what the sheet sends is what the palette calls "on the item's sheet".
    [Theory]
    [InlineData("CloseItem:done", true)]
    [InlineData("CloseItem:cancelled", true)]
    [InlineData("CloseItem", false)]
    [InlineData("CloseItem:other", false)]
    [InlineData("CreateItem", false)]
    public void Only_the_actions_the_sheet_sends_are_the_sheets(string action, bool sheets) =>
        Assert.Equal(sheets, ItemSheetViewModel.IsSheetAction(action));

    [Fact]
    public async Task Every_action_the_sheet_calls_its_own_has_a_sender_there()
    {
        var sheet = await Sheet();
        await sheet.Open(_api, A.NewItem, Token);

        Assert.Contains(sheet.Actions, c => ItemSheetViewModel.IsSheetAction(c.Action));
        Assert.All(sheet.Actions.Where(c => ItemSheetViewModel.IsSheetAction(c.Action)), c => Assert.True(c.IsSendable));
    }
}
