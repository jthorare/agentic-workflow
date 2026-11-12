using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Queue;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Queue;

/// <summary>
/// §8's guards and filter on the queue: new rows are held while the pointer is over a panel (an "N new" pill), single
/// keys are ignored for 500 ms after a refresh moved the selection on its own, and `/` filters by id, title and repo.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class QueueKeysTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    // P3 serves no waitingOnYou rows; the P4 extra adds the needs-decision row (a pending Decide) these behaviours need.
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), QueueView = RouteAnswer.SpecShaped("queue", "extra-p4-waiting") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<QueueViewModel> Queue()
    {
        var catalog = new CatalogViewModel(new FakeClipboard(), _clock);
        catalog.Show(_api, _server, (await _server.Commands(_api, Token)).Value);
        var queue = new QueueViewModel(_server, catalog, _clock);
        await queue.Refresh(_api, Token);
        return queue;
    }

    // §8: while the pointer is over a panel, new rows are held, not shifted in under it; the pill says how many.
    [Fact]
    public async Task Rows_are_held_while_the_pointer_is_over_a_panel_and_shown_on_leave()
    {
        var queue = await Queue();
        queue.Hold(holding: true);

        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p11-running");
        await queue.Refresh(_api, Token);

        Assert.Equal((5, true, "7 new"), (queue.Total, queue.HasHeld, queue.HeldText));

        queue.Hold(holding: false);
        Assert.Equal((12, false), (queue.Total, queue.HasHeld));
    }

    // The pill shows the held rows at once, even while the pointer stays.
    [Fact]
    public async Task The_pill_shows_the_held_rows_while_the_pointer_stays()
    {
        var queue = await Queue();
        queue.Hold(holding: true);
        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p11-running");
        await queue.Refresh(_api, Token);

        queue.ShowHeld(QueueScope.Page);

        Assert.Equal((12, false), (queue.Total, queue.HasHeld));
        _server.QueueView = RouteAnswer.SpecShaped("queue", "fixture");
        await queue.Refresh(_api, Token);
        Assert.Equal((4, false), (queue.Total, queue.HasHeld));   // L4: removals are not held
    }

    // a11y-3: the pill hides when it is pressed, taking focus with it: focus goes to the first row it showed.
    [Fact]
    public async Task The_pill_asks_for_focus_on_the_first_row_it_shows()
    {
        var queue = await Queue();
        queue.Hold(holding: true);
        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p11-running");
        await queue.Refresh(_api, Token);
        var asked = new List<FocusRequestedEventArgs>();
        var known = queue.Groups.SelectMany(g => g.Rows).Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        queue.FocusRequested += (_, e) => asked.Add(e);

        queue.ShowHeld(QueueScope.Page);

        var first = queue.Groups.Where(g => g.IsVisible).SelectMany(g => g.Rows).First(r => !known.Contains(r.Id));
        Assert.Equal([(first.Id, false)], asked.Select(a => (a.ItemId, a.OnlyIfLost)));
    }

    // corr-6: "N new" and "<id> cleared" in one refresh are one sentence, spoken once: the flag first, then the text, never "".
    [Fact]
    public async Task New_rows_and_a_cleared_row_in_one_refresh_are_spoken_as_one_sentence()
    {
        var queue = await Queue();
        queue.Select(A.NewItem, QueueScope.Page);
        queue.Hold(holding: true);
        var view = System.Text.Json.Nodes.JsonNode.Parse(GoldenFiles.SpecShaped("queue", "extra-p11-running"))!;
        foreach (var group in view["groups"]!.AsArray())
        {
            var items = group!["items"]!.AsArray();
            items.Where(i => (string?)i!["id"] == A.NewItem).ToList().ForEach(i => items.Remove(i));
        }

        var states = new List<(bool Live, string Text)>();
        queue.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(QueueViewModel.IsAnnouncing), StringComparison.Ordinal) || string.Equals(e.PropertyName, nameof(QueueViewModel.Announcement), StringComparison.Ordinal))
            {
                states.Add((queue.IsAnnouncing, queue.Announcement));
            }
        };

        _server.QueueView = new RouteAnswer(System.Net.HttpStatusCode.OK, view.ToJsonString());
        await queue.Refresh(_api, Token);

        var sentence = Labels.NewAndCleared(queue.HeldText, A.NewItem);
        Assert.True(queue.HasHeld);
        Assert.Equal((true, sentence), (queue.IsAnnouncing, queue.Announcement));
        Assert.DoesNotContain(states, s => s.Text.Length == 0);
        Assert.Equal(1, states.Count(s => s.Live && s.Text == sentence));
        Assert.DoesNotContain(states, s => !s.Live && s.Text == sentence);
        Assert.DoesNotContain(states, s => s.Text == queue.HeldText || s.Text == Labels.Cleared(A.NewItem));
    }

    // Leaving the panel shows the held rows too, but nobody pressed the pill: focus stays where it is.
    [Fact]
    public async Task Leaving_the_panel_shows_the_held_rows_without_asking_for_focus()
    {
        var queue = await Queue();
        queue.Hold(holding: true);
        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p11-running");
        await queue.Refresh(_api, Token);
        var asked = 0;
        queue.FocusRequested += (_, _) => asked++;

        queue.Hold(holding: false);

        Assert.Equal(0, asked);
    }

    // L4 (§8): hold only keeps rows from shifting in under the pointer. A removal is shown at once: no pill, nothing stale to click.
    [Fact]
    public async Task A_held_refresh_without_new_rows_shows_no_pill_and_applies_the_removals()
    {
        var queue = await Queue();
        queue.Hold(holding: true);
        _server.QueueView = RouteAnswer.SpecShaped("queue", "empty");
        await queue.Refresh(_api, Token);

        Assert.Equal((0, false), (queue.Total, queue.HasHeld));
        queue.Hold(holding: false);
        Assert.Equal(0, queue.Total);
    }

    // L4: an in-place update (a new title) and a removal show at once while only the new rows wait behind the pill, which is spoken.
    [Fact]
    public async Task A_held_refresh_applies_updates_and_removals_and_holds_only_the_new_rows()
    {
        var queue = await Queue();
        queue.Hold(holding: true);
        var row = queue.Groups[3].Rows[3];

        var added = GoldenFiles.SpecShaped("queue", "extra-p11-running").Replace("Try the queue", "Renamed item", StringComparison.Ordinal);
        _server.QueueView = new RouteAnswer(System.Net.HttpStatusCode.OK, added);
        await queue.Refresh(_api, Token);

        Assert.Equal(("Renamed item", 5, true, "7 new"), (row.Title, queue.Total, queue.HasHeld, queue.HeldText));
        Assert.Equal((true, "7 new"), (queue.IsAnnouncing, queue.Announcement));

        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p4-waiting");
        await queue.Refresh(_api, Token);
        Assert.Equal((false, 5), (queue.HasHeld, queue.Total));
    }

    // §8 key guard: for 500 ms after a refresh moved the selection on its own, single keys are ignored.
    [Fact]
    public async Task A_selection_moved_by_a_refresh_guards_single_keys_for_500_ms()
    {
        var queue = await Queue();
        queue.Select(A.NewItem, QueueScope.Page);
        Assert.False(queue.IsGuarded);

        _server.QueueView = new RouteAnswer(System.Net.HttpStatusCode.OK, GoldenFiles.SpecShaped("queue", "fixture").Replace(A.NewItem, "ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a3ff", StringComparison.Ordinal));
        await queue.Refresh(_api, Token);

        Assert.True(queue.IsGuarded);
        _clock.Advance(TimeSpan.FromMilliseconds(499));
        Assert.True(queue.IsGuarded);
        _clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.False(queue.IsGuarded);
    }

    // §8 `/`: filter the list by id, title or repo; nothing matching says so.
    [Fact]
    public async Task The_filter_keeps_rows_whose_id_title_or_repo_match()
    {
        var queue = await Queue();

        queue.FilterText = "RETRY";
        Assert.Equal(["Retry storm in outbox dispatcher"], queue.Groups.SelectMany(g => g.Rows).Select(r => r.Title), StringComparer.Ordinal);
        Assert.Equal([false, false, false, true], queue.Groups.Select(g => g.IsVisible));

        queue.FilterText = "sindri-docs";
        Assert.Equal("Context map review for tabs.layout", Assert.Single(queue.Groups.SelectMany(g => g.Rows)).Title);

        queue.FilterText = "nothing like this";
        Assert.Equal((true, "Nothing matches"), (queue.IsEmpty, queue.EmptyText));
        Assert.Equal(5, queue.Total);

        queue.ClearFilter();
        Assert.Equal((false, string.Empty, 5), (queue.IsFiltering, queue.FilterText, queue.Groups.Sum(g => g.Rows.Count)));
    }

    // §8 `Y`: the selected row's CLI, from its primary action's catalog row.
    [Fact]
    public async Task The_selected_rows_cli_is_its_primary_actions()
    {
        var queue = await Queue();

        Assert.Null(queue.SelectedCopy());
        queue.Select(A.DecisionItem, QueueScope.Page);
        Assert.Equal("sindri decide <decisionId> <choice> [--note <note>]", queue.SelectedCopy()?.Cli);
        queue.Select(A.NewItem, QueueScope.Page);
        Assert.Null(queue.SelectedCopy());
    }

    // L2 (§8): J / K walk the rows of the page they are pressed on: Home's panels as drawn, the Queue page's groups as drawn.
    [Fact]
    public async Task J_walks_homes_rows_as_drawn_and_the_pages_expanded_groups()
    {
        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p11-running");
        var queue = await Queue();
        var home = queue.RunningRows.Concat(queue.QueuedRows).Select(r => r.Id).ToList();

        var walked = new List<string?>();
        for (var i = 0; i < home.Count; i++)
        {
            queue.Move(1, QueueScope.Home);
            walked.Add(queue.SelectedId);
        }

        Assert.Equal(home, walked.Select(w => w), StringComparer.Ordinal);

        queue.Select(string.Empty, QueueScope.Page);
        queue.Groups.Single(g => g.Group == "queued").IsExpanded = false;
        var page = queue.Groups.Where(g => g.IsVisible && g.IsExpanded).SelectMany(g => g.Rows).Select(r => r.Id).ToList();
        var seen = new List<string?>();
        for (var i = 0; i < page.Count + 2; i++)
        {
            queue.Move(1, QueueScope.Page);
            seen.Add(queue.SelectedId);
        }

        Assert.Equal([.. page, page[^1], page[^1]], seen.Select(w => w), StringComparer.Ordinal);
    }

    // L2: a filter change moves the selection off a row it hides (silently: no announcement, no key guard).
    [Fact]
    public async Task A_filter_change_clears_a_selection_it_hides_silently()
    {
        var queue = await Queue();
        queue.Select(A.NewItem, QueueScope.Page);

        queue.FilterText = "Try the";
        Assert.Equal(A.NewItem, queue.SelectedId);

        queue.FilterText = "RETRY";
        Assert.Equal((null, Labels.Sindri, false), (queue.SelectedId, queue.Announcement, queue.IsGuarded));

        queue.Select(A.NewItem, QueueScope.Page);
        queue.ClearFilter();
        queue.Select(A.NewItem, QueueScope.Page);
        queue.Filter(QueueFilter.Running);
        Assert.Null(queue.SelectedId);
    }

    // L2: a row the filter hides is not a row that is gone: a refresh keeps it selected and says nothing.
    [Fact]
    public async Task A_refresh_keeps_a_selection_the_filter_hides()
    {
        var queue = await Queue();
        queue.FilterText = "RETRY";
        queue.Select(A.NewItem, QueueScope.Page);

        await queue.Refresh(_api, Token);

        Assert.Equal((A.NewItem, false, Labels.Sindri), (queue.SelectedId, queue.IsAnnouncing, queue.Announcement));
    }

    // a11y-11: the title button is named for what it does.
    [Fact]
    public async Task A_rows_title_button_is_named_open_and_its_id()
    {
        var queue = await Queue();

        Assert.Equal("Open Try the queue", queue.Groups[3].Rows[3].OpenName);
    }

    // The golden queue with the item removed (the projector cleared it), or its primary action set.
    private static RouteAnswer Edited(string view, Action<System.Text.Json.Nodes.JsonArray> edit)
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(GoldenFiles.SpecShaped("queue", view))!;
        foreach (var group in root["groups"]!.AsArray())
        {
            edit(group!["items"]!.AsArray());
        }

        return new RouteAnswer(System.Net.HttpStatusCode.OK, root.ToJsonString());
    }

    internal static RouteAnswer Without(string view, string itemId) =>
        Edited(view, items => items.Where(i => i!["id"]!.GetValue<string>() == itemId).ToList().ForEach(i => items.Remove(i)));

    private static string Q(int n) => "ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a30" + n.ToString(System.Globalization.CultureInfo.InvariantCulture);

    // What the gated polite region said: each text set while it was live (flag first, then the text).
    private static List<string> Gated(QueueViewModel queue)
    {
        var said = new List<string>();
        queue.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(QueueViewModel.Announcement), StringComparison.Ordinal) && queue.IsAnnouncing)
            {
                said.Add(queue.Announcement);
            }
        };
        return said;
    }

    private static List<string> FocusAsked(QueueViewModel queue)
    {
        var asked = new List<string>();
        queue.FocusRequested += (_, e) => asked.Add(e.ItemId);
        return asked;
    }

    // corr-3 (G R, Home): the Queued panel is Home's whatever the Queue page's filter shows; its cleared row passes to the next queued row.
    [Fact]
    public async Task A_queued_row_cleared_on_home_with_the_running_filter_on_selects_the_next_queued_row()
    {
        var queue = await Queue();
        queue.Filter(QueueFilter.Running);
        queue.Move(1, QueueScope.Home);
        queue.Move(1, QueueScope.Home);
        Assert.Equal(Q(2), queue.SelectedId);
        var asked = FocusAsked(queue);

        _server.QueueView = Without("extra-p4-waiting", Q(2));
        await queue.Refresh(_api, Token);

        Assert.Equal((Q(3), true), (queue.SelectedId, queue.QueuedRows.Single(r => r.Id == Q(3)).IsSelected));
        Assert.Equal([Q(3)], asked);
    }

    // corr-3 (FilterText set, Home): Home's panels ignore the Queue page's text filter; so does the highlight and Y.
    [Fact]
    public async Task A_row_the_queue_pages_text_filter_hides_is_still_selected_and_copyable_on_home()
    {
        _server.QueueView = Edited("extra-p4-waiting", items =>
        {
            foreach (var item in items.Where(i => i!["id"]!.GetValue<string>() == Q(1)))
            {
                item!["primaryAction"] = "Decide";
            }
        });
        var queue = await Queue();
        queue.FilterText = "no row has this";

        queue.Move(1, QueueScope.Home);

        Assert.Equal((Q(1), true), (queue.SelectedId, queue.QueuedRows[0].IsSelected));
        Assert.NotNull(queue.SelectedCopy());
    }

    // The view with an item taken out of its group and put in another (the projector re-grouped it).
    internal static RouteAnswer Regrouped(string view, string itemId, string toGroup, int clones = 0)
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(GoldenFiles.SpecShaped("queue", view))!;
        var groups = root["groups"]!.AsArray();
        System.Text.Json.Nodes.JsonNode? moved = null;
        var template = System.Text.Json.Nodes.JsonNode.Parse(groups.Single(g => g!["group"]!.GetValue<string>() == "withYou")!["items"]!.AsArray()[0]!.ToJsonString())!;
        foreach (var group in groups)
        {
            var items = group!["items"]!.AsArray();
            foreach (var item in items.Where(i => i!["id"]!.GetValue<string>() == itemId).ToList())
            {
                _ = items.Remove(item);
                moved = item;
            }
        }

        var target = groups.Single(g => g!["group"]!.GetValue<string>() == toGroup)!["items"]!.AsArray();
        if (moved is not null)
        {
            target.Add(moved);
        }

        for (var n = 0; n < clones; n++)
        {
            var copy = System.Text.Json.Nodes.JsonNode.Parse(template.ToJsonString())!;
            copy["id"] = "ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a9" + n.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
            groups.Single(g => g!["group"]!.GetValue<string>() == "withYou")!["items"]!.AsArray().Insert(0, copy);
        }

        return new RouteAnswer(System.Net.HttpStatusCode.OK, root.ToJsonString());
    }

    internal static string Running(int n) => "ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2e70" + n.ToString(System.Globalization.CultureInfo.InvariantCulture);

    // corr-1 / a11y-0 (a): a focused row Home draws that is served in a group Home does not draw has left the page: like a cleared row.
    [Fact]
    public async Task A_focused_home_row_served_in_a_group_home_does_not_draw_passes_to_the_next_row_and_is_spoken_once()
    {
        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p11-running");
        var queue = await Queue();
        queue.Move(1, QueueScope.Home);
        queue.Move(1, QueueScope.Home);
        Assert.Equal(Running(1), queue.SelectedId);
        var asked = new List<FocusRequestedEventArgs>();
        queue.FocusRequested += (_, e) => asked.Add(e);
        var gated = Gated(queue);

        _server.QueueView = Regrouped("extra-p11-running", Running(1), "waitingOnYou");
        await queue.Refresh(_api, Token);

        Assert.Equal([Labels.Cleared(Running(1))], gated);
        var drawn = queue.RunningRows.Concat(queue.QueuedRows).Select(r => r.Id).ToList();
        Assert.Contains(queue.SelectedId!, drawn, StringComparer.Ordinal);
        Assert.NotEqual(Running(1), queue.SelectedId, StringComparer.Ordinal);
        Assert.Equal([(queue.SelectedId!, true)], asked.Select(a => (a.ItemId, a.OnlyIfLost)));
        Assert.Equal((true, Labels.Cleared(Running(1))), (queue.IsAnnouncing, queue.Announcement));
        Assert.True(queue.IsGuarded);
    }

    // (b): five new With you rows push the focused row past Home's cap of five.
    [Fact]
    public async Task A_focused_home_row_pushed_past_the_cap_passes_to_a_row_that_is_drawn()
    {
        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p11-running");
        var queue = await Queue();
        for (var i = 0; i < 5; i++)
        {
            queue.Move(1, QueueScope.Home);
        }

        var last = queue.RunningRows[^1].Id;
        Assert.Equal(last, queue.SelectedId);
        var asked = FocusAsked(queue);
        var gated = Gated(queue);

        _server.QueueView = Regrouped("extra-p11-running", string.Empty, "withYou", clones: 5);
        await queue.Refresh(_api, Token);

        Assert.Equal([Labels.Cleared(last)], gated);
        var drawn = queue.RunningRows.Concat(queue.QueuedRows).Select(r => r.Id).ToList();
        Assert.DoesNotContain(last, drawn, StringComparer.Ordinal);
        Assert.Contains(queue.SelectedId!, drawn, StringComparer.Ordinal);
        Assert.Equal([queue.SelectedId!], asked);
        Assert.Equal((true, Labels.Cleared(last)), (queue.IsAnnouncing, queue.Announcement));
    }

    // (c): G R on the Queue page; the focused running row is served in With you, a group the filter hides.
    [Fact]
    public async Task A_focused_page_row_served_in_a_group_the_filter_hides_passes_to_the_next_running_row()
    {
        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p11-running");
        var queue = await Queue();
        queue.Filter(QueueFilter.Running);
        queue.Move(1, QueueScope.Page);
        queue.Move(1, QueueScope.Page);
        Assert.Equal(Running(2), queue.SelectedId);
        var asked = FocusAsked(queue);
        var gated = Gated(queue);

        _server.QueueView = Regrouped("extra-p11-running", Running(2), "withYou");
        await queue.Refresh(_api, Token);

        Assert.Equal([Labels.Cleared(Running(2))], gated);
        var shown = queue.Groups.Where(g => g.IsVisible).SelectMany(g => g.Rows).Select(r => r.Id).ToList();
        Assert.Contains(queue.SelectedId!, shown, StringComparer.Ordinal);
        Assert.NotEqual(Running(2), queue.SelectedId, StringComparer.Ordinal);
        Assert.Equal([queue.SelectedId!], asked);
        Assert.Equal(Labels.Cleared(Running(2)), queue.Announcement);
    }

    // R1: the selection's scope follows the page the shell SHOWS. G R, J on a running row, G H: Home draws With you too, so a
    // row served in With you is still on the page shown and keeps its selection, silently.
    [Fact]
    public async Task A_selection_made_under_a_filter_follows_the_page_the_shell_shows_when_it_goes_home()
    {
        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p11-running");
        var queue = await Queue();
        queue.Filter(QueueFilter.Running);
        queue.Move(1, QueueScope.Page);
        queue.Move(1, QueueScope.Page);
        Assert.Equal(Running(2), queue.SelectedId);
        var asked = FocusAsked(queue);
        queue.PageShown(QueueScope.Home);

        _server.QueueView = Regrouped("extra-p11-running", Running(2), "withYou");
        await queue.Refresh(_api, Token);

        Assert.Equal(Running(2), queue.SelectedId);
        Assert.Equal((false, Labels.Sindri, false), (queue.IsAnnouncing, queue.Announcement, queue.IsGuarded));
        Assert.Empty(asked);
    }

    // R1: select on Home, G Q; the refresh that pushes the row past Home's cap is no concern of the Queue page, which still draws it.
    [Fact]
    public async Task A_row_pushed_past_homes_cap_while_the_queue_page_is_shown_keeps_its_selection_silently()
    {
        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p11-running");
        var queue = await Queue();
        for (var i = 0; i < 5; i++)
        {
            queue.Move(1, QueueScope.Home);
        }

        var last = queue.RunningRows[^1].Id;
        queue.PageShown(QueueScope.Page);
        var asked = FocusAsked(queue);

        _server.QueueView = Regrouped("extra-p11-running", string.Empty, "withYou", clones: 5);
        await queue.Refresh(_api, Token);

        Assert.Equal(last, queue.SelectedId);
        Assert.Equal((false, Labels.Sindri, false), (queue.IsAnnouncing, queue.Announcement, queue.IsGuarded));
        Assert.Empty(asked);
    }

    // R1: on any other page (Decisions, Services...) neither list is shown: a served row keeps its selection, silently.
    [Fact]
    public async Task A_served_row_keeps_its_selection_silently_while_another_page_is_shown()
    {
        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p11-running");
        var queue = await Queue();
        queue.Move(1, QueueScope.Home);
        queue.Move(1, QueueScope.Home);
        Assert.Equal(Running(1), queue.SelectedId);
        var asked = FocusAsked(queue);
        queue.PageShown(scope: null);

        _server.QueueView = Regrouped("extra-p11-running", Running(1), "waitingOnYou");
        await queue.Refresh(_api, Token);

        Assert.Equal(Running(1), queue.SelectedId);
        Assert.Equal((false, Labels.Sindri, false), (queue.IsAnnouncing, queue.Announcement, queue.IsGuarded));
        Assert.Empty(asked);
    }

    // V1 (a11y-0): the selected row leaves the queue while a page without rows is shown: the selection passes on, silently.
    [Fact]
    public async Task A_selected_row_that_leaves_the_queue_while_another_page_is_shown_settles_silently()
    {
        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p11-running");
        var queue = await Queue();
        queue.Move(1, QueueScope.Home);
        queue.Move(1, QueueScope.Home);
        Assert.Equal(Running(1), queue.SelectedId);
        var asked = FocusAsked(queue);
        queue.PageShown(scope: null);

        _server.QueueView = Without("extra-p11-running", Running(1));
        await queue.Refresh(_api, Token);

        Assert.NotEqual(Running(1), queue.SelectedId, StringComparer.Ordinal);
        Assert.Equal((false, Labels.Sindri, false), (queue.IsAnnouncing, queue.Announcement, queue.IsGuarded));
        Assert.Empty(asked);
    }

    // The golden fixture's queued group grown to n rows (clones of its first), appended in order.
    private static RouteAnswer QueuedGrownTo(int n) => Edited("fixture", items =>
    {
        if (items.Count == 0 || !items[0]!["id"]!.GetValue<string>().StartsWith("ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a30", StringComparison.Ordinal))
        {
            return;
        }

        for (var i = items.Count; i < n; i++)
        {
            var copy = System.Text.Json.Nodes.JsonNode.Parse(items[0]!.ToJsonString())!;
            copy["id"] = "ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a4" + i.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
            items.Add(copy);
        }
    });

    // V2 (a11y-1): a row past Home's cap is drawn nowhere, so nothing under the pointer can move: no pill, the view applies at once.
    [Fact]
    public async Task A_queued_row_past_homes_cap_is_not_held_and_the_view_applies_at_once()
    {
        _server.QueueView = QueuedGrownTo(5);
        var queue = await Queue();
        queue.PageShown(QueueScope.Home);
        Assert.Equal(5, queue.QueuedRows.Count);
        queue.Hold(holding: true);

        _server.QueueView = QueuedGrownTo(6);
        await queue.Refresh(_api, Token);

        Assert.Equal((false, 6, 5, Labels.QueuedHeader(6)), (queue.HasHeld, queue.Total, queue.QueuedRows.Count, queue.QueuedHeader));
        Assert.Equal((false, Labels.Sindri), (queue.IsAnnouncing, queue.Announcement));
    }

    // One new row Home draws and one past its cap: the pill counts the drawn one.
    [Fact]
    public async Task The_pill_counts_only_the_new_rows_the_page_draws()
    {
        _server.QueueView = QueuedGrownTo(4);
        var queue = await Queue();
        queue.PageShown(QueueScope.Home);
        queue.Hold(holding: true);

        _server.QueueView = QueuedGrownTo(6);
        await queue.Refresh(_api, Token);

        Assert.Equal((true, "1 new", 4), (queue.HasHeld, queue.HeldText, queue.Total));
        queue.Hold(holding: false);
        Assert.Equal((false, 6, 5), (queue.HasHeld, queue.Total, queue.QueuedRows.Count));
    }

    // R2: a collapsed group draws no rows: a focused running row served into one has left the page, like a cleared row.
    [Fact]
    public async Task A_focused_page_row_served_into_a_collapsed_group_passes_to_the_next_running_row_and_is_spoken_once()
    {
        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p11-running");
        var queue = await Queue();
        queue.Groups.Single(g => g.Group == "queued").IsExpanded = false;
        queue.Select(Running(2), QueueScope.Page);
        var asked = new List<FocusRequestedEventArgs>();
        queue.FocusRequested += (_, e) => asked.Add(e);

        _server.QueueView = Regrouped("extra-p11-running", Running(2), "queued");
        await queue.Refresh(_api, Token);

        var drawn = queue.Groups.Where(g => g.IsVisible && g.IsExpanded).SelectMany(g => g.Rows).Select(r => r.Id).ToList();
        Assert.Contains(queue.SelectedId!, drawn, StringComparer.Ordinal);
        Assert.NotEqual(Running(2), queue.SelectedId, StringComparer.Ordinal);
        Assert.Equal([(queue.SelectedId!, true)], asked.Select(a => (a.ItemId, a.OnlyIfLost)));
        Assert.Equal((true, Labels.Cleared(Running(2))), (queue.IsAnnouncing, queue.Announcement));
    }

    // narrow-1: a collapsed group with no rows shows no heading; the first row to arrive in it brings the heading, so the page
    // moves under the pointer and the row is held like any other the page draws.
    [Fact]
    public async Task A_new_row_in_an_empty_collapsed_group_is_held()
    {
        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p11-running");
        var queue = await Queue();
        queue.PageShown(QueueScope.Page);
        var withYou = queue.Groups.Single(g => g.Group == "withYou");
        var served = withYou.Rows[0].Id;
        withYou.IsExpanded = false;
        _server.QueueView = Regrouped("extra-p11-running", served, "waitingOnYou");
        await queue.Refresh(_api, Token);
        queue.Hold(holding: true);

        _server.QueueView = Regrouped("extra-p11-running", served, "waitingOnYou", clones: 1);
        await queue.Refresh(_api, Token);

        Assert.Equal((true, "1 new"), (queue.HasHeld, queue.HeldText));
    }

    // R3: the pill's press asks for the rows: focus goes to the first new one, once. The selected row it pushes past Home's cap
    // is no "cleared" row: nothing is spoken for it, no second focus move, no key guard.
    [Fact]
    public async Task The_pill_press_moves_focus_once_to_the_first_new_row_even_when_it_pushes_the_selection_past_the_cap()
    {
        _server.QueueView = RouteAnswer.SpecShaped("queue", "extra-p11-running");
        var queue = await Queue();
        for (var i = 0; i < 5; i++)
        {
            queue.Move(1, QueueScope.Home);
        }

        var last = queue.RunningRows[^1].Id;
        queue.Hold(holding: true);
        _server.QueueView = Regrouped("extra-p11-running", string.Empty, "withYou", clones: 5);
        await queue.Refresh(_api, Token);
        Assert.True(queue.HasHeld);
        var held = (queue.IsAnnouncing, queue.Announcement);
        var asked = new List<FocusRequestedEventArgs>();
        queue.FocusRequested += (_, e) => asked.Add(e);

        queue.ShowHeld(QueueScope.Home);

        Assert.DoesNotContain(last, queue.RunningRows.Select(r => r.Id), StringComparer.Ordinal);
        var focus = Assert.Single(asked);
        Assert.False(focus.OnlyIfLost);
        Assert.Equal(queue.RunningRows[0].Id, focus.ItemId);
        Assert.Equal(focus.ItemId, queue.SelectedId);
        Assert.Equal(held, (queue.IsAnnouncing, queue.Announcement));
        Assert.False(queue.IsGuarded);
    }
}
