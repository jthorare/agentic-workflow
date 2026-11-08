using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>The inbox page's view model (needs-you spec §2, slice 1): the grouped list, the selection that survives a re-read, and the request card's plain words.</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouPageViewModelTests
{
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeSindriApi _server = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private Task<(NeedsYouViewModel List, NeedsYouPageViewModel Page)> Page(params NeedsYouEntry[] rows) => Page(confirm: null, rows);

    private async Task<(NeedsYouViewModel List, NeedsYouPageViewModel Page)> Page(NeedsYouConfirm? confirm, params NeedsYouEntry[] rows)
    {
        var list = new NeedsYouViewModel(new Preferences(new InMemoryPreferencesStore()), new FakeMotionPreference(), _clock);
        await list.Restore(Token);
        list.Update(new NeedsYouFeed(rows, rows.Length == 0 ? "Nothing needs you. 1 running, 2 queued." : ""));
        return (list, new NeedsYouPageViewModel(list, _clock, _clipboard, new FakeLauncher(), confirm));
    }

    [Fact]
    public async Task Groups_carry_their_heading_and_count_and_hold_the_row_view_models_of_the_list()
    {
        var (list, page) = await Page(NeedsYouRows.Failure(), NeedsYouRows.Decision(), NeedsYouRows.Park("setup:WFI-2"));

        Assert.Equal([("Agent decisions", "1"), ("Settings approvals", "1"), ("Something failed", "1")], page.Groups.Select(g => (g.Heading, g.CountText)));
        Assert.Same(list.Seen.Rows.Single(r => r.Id == "setup:WFI-2"), page.Groups[1].Rows.Single());
    }

    [Fact]
    public async Task The_header_counts_the_rows()
    {
        var (_, page) = await Page(NeedsYouRows.Decision(), NeedsYouRows.Failure(), NeedsYouRows.Profile());

        Assert.Equal("3 waiting", page.CountText);
    }

    [Fact]
    public async Task Opening_selects_the_first_row_of_the_first_group_unless_one_is_selected()
    {
        var (list, page) = await Page(NeedsYouRows.Failure(), NeedsYouRows.Decision());

        page.SelectFirst();
        Assert.Equal("decision:DEC-1", list.SelectedId);

        list.Select("failure:service:index");
        page.SelectFirst();
        Assert.Equal("failure:service:index", list.SelectedId);
    }

    [Fact]
    public async Task J_and_K_walk_the_groups_in_the_pages_order_and_stop_at_the_ends()
    {
        var (list, page) = await Page(NeedsYouRows.Failure(), NeedsYouRows.Decision(), NeedsYouRows.Park("setup:WFI-2"));
        page.SelectFirst();

        var seen = new List<string?> { list.SelectedId };
        _ = page.Move(1);
        seen.Add(list.SelectedId);
        _ = page.Move(1);
        seen.Add(list.SelectedId);
        _ = page.Move(1);
        seen.Add(list.SelectedId);
        _ = page.Move(-1);
        seen.Add(list.SelectedId);

        Assert.Equal(["decision:DEC-1", "setup:WFI-2", "failure:service:index", "failure:service:index", "setup:WFI-2"], seen);
    }

    [Fact]
    public async Task The_card_follows_the_selected_row_in_plain_words()
    {
        var (list, page) = await Page(NeedsYouRows.Park("setup:WFI-2", minutes: 351), NeedsYouRows.Decision());

        list.Select("setup:WFI-2");

        Assert.Equal("setup:WFI-2", page.Selected?.Id);
        Assert.Equal(("Settings change", "Waiting for you", "waiting 5h 51m"), (page.KindWord, page.FooterLeft, page.FooterRight));

        list.Select("decision:DEC-1");
        Assert.Equal(("Agent decision", "waiting 26m"), (page.KindWord, page.FooterRight));
    }

    [Fact]
    public async Task The_card_names_every_kind_by_its_own_word()
    {
        var (list, page) = await Page(NeedsYouRows.Profile(), NeedsYouRows.Failure());

        var words = new List<string>();
        foreach (var id in new[] { "profile:drift", "failure:service:index" })
        {
            list.Select(id);
            words.Add(page.KindWord);
        }

        Assert.Equal(["Settings change", "Something failed"], words);
    }

    [Fact]
    public async Task A_selected_row_stays_selected_across_a_re_read_by_its_id()
    {
        var (list, page) = await Page(NeedsYouRows.Decision(), NeedsYouRows.Failure());
        list.Select("failure:service:index");

        list.Update(new NeedsYouFeed([NeedsYouRows.Failure(), NeedsYouRows.Decision(), NeedsYouRows.Park("setup:WFI-2")], ""));

        Assert.Equal("failure:service:index", page.Selected?.Id);
    }

    [Fact]
    public async Task A_row_that_clears_keeps_its_card_cleared_while_the_selection_moves_on()
    {
        var (list, page) = await Page(NeedsYouRows.Decision(), NeedsYouRows.Failure());
        list.Select("decision:DEC-1");

        list.Update(new NeedsYouFeed([NeedsYouRows.Failure()], ""));

        Assert.Equal("failure:service:index", page.Selected?.Id);
        Assert.Equal(("decision:DEC-1", true, "REQ-01JD4 cleared"), (page.Card?.Id, page.IsCleared, page.ClearedText));
    }

    [Fact]
    public async Task Moving_or_clicking_leaves_the_cleared_card()
    {
        var (list, page) = await Page(NeedsYouRows.Decision(), NeedsYouRows.Failure(), NeedsYouRows.Park("setup:WFI-2"));
        list.Select("decision:DEC-1");
        list.Update(new NeedsYouFeed([NeedsYouRows.Failure(), NeedsYouRows.Park("setup:WFI-2")], ""));
        Assert.True(page.IsCleared);

        _ = page.Move(1);
        Assert.Equal((false, page.Selected?.Id), (page.IsCleared, page.Card?.Id));

        list.Select("failure:service:index");
        list.Update(new NeedsYouFeed([NeedsYouRows.Park("setup:WFI-2")], ""));
        Assert.True(page.IsCleared);
        page.Select("setup:WFI-2");
        Assert.False(page.IsCleared);
    }

    [Fact]
    public async Task An_inbox_opened_empty_selects_the_first_row_when_rows_arrive()
    {
        var (list, page) = await Page();
        page.Active = true;

        list.Update(new NeedsYouFeed([NeedsYouRows.Failure(), NeedsYouRows.Decision()], ""));

        Assert.Equal("decision:DEC-1", page.Selected?.Id);
    }

    [Fact]
    public async Task The_footer_wait_follows_the_clock_on_the_next_read_and_is_named_whole()
    {
        var (list, page) = await Page(NeedsYouRows.Decision());
        list.Select("decision:DEC-1");
        Assert.Equal(("waiting 26m", "Waiting for you, waiting 26m"), (page.FooterRight, page.FooterName));

        _clock.Advance(TimeSpan.FromMinutes(4));
        list.Update(new NeedsYouFeed([NeedsYouRows.Decision()], ""));

        Assert.Equal("waiting 30m", page.FooterRight);
    }

    [Fact]
    public async Task The_selected_row_says_so_in_its_name()
    {
        var (list, _) = await Page(NeedsYouRows.Decision(), NeedsYouRows.Failure());

        list.Select("failure:service:index");

        var names = list.Seen.Rows.ToDictionary(r => r.Id, r => r.InboxName, StringComparer.Ordinal);
        Assert.EndsWith(", selected", names["failure:service:index"], StringComparison.Ordinal);
        Assert.DoesNotContain("selected", names["decision:DEC-1"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_no_rows_there_is_no_card_and_no_group()
    {
        var (_, page) = await Page();

        Assert.Empty(page.Groups);
        Assert.Null(page.Selected);
        Assert.Equal("0 waiting", page.CountText);
    }
    [Fact]
    public async Task The_action_bar_follows_the_card_and_a_new_request_starts_it_fresh()
    {
        var (list, page) = await Page(NeedsYouRows.Decision(), NeedsYouRows.Failure());
        list.Select(NeedsYouRows.Decision().Id);
        await page.Bar.Loaded;
        page.Bar.ReadToEnd = true;
        await page.Bar.PressSendBack();
        Assert.Equal(NeedsYouBarMode.SendingBack, page.Bar.Mode);

        _ = page.Move(1);

        Assert.Equal((NeedsYouBarMode.Pending, false, "Approve"), (page.Bar.Mode, page.Bar.ReadToEnd, page.Bar.ApproveWord));
    }

    [Fact]
    public async Task Approving_replaces_the_card_with_the_done_card_until_the_user_moves_on()
    {
        var (list, page) = await Page(new NeedsYouConfirm(() => new Uri("http://127.0.0.1:7410/"), _server), NeedsYouRows.ProfileReady(), NeedsYouRows.Decision());
        list.Select(NeedsYouRows.ProfileReady().Id);
        await page.Bar.Loaded;

        await page.Bar.PressApprove();

        Assert.Equal((true, false, Labels.NeedsYouApprovedTitle(NeedsYouRows.ProfileReady().Title)), (page.IsDone, page.ShowsRequest, page.Bar.DoneTitle));
        _ = page.Move(-1);   // the decisions group is listed before the settings one, so the approved profile is the last row and the other way is up
        Assert.Equal((false, true), (page.IsDone, page.ShowsRequest));
    }

    [Fact]
    public async Task A_request_that_left_the_list_keeps_its_card_and_quiets_the_bar()
    {
        var (list, page) = await Page(new NeedsYouConfirm(() => new Uri("http://127.0.0.1:7410/"), _server), NeedsYouRows.ProfileReady(), NeedsYouRows.Decision());
        list.Select(NeedsYouRows.ProfileReady().Id);
        await page.Bar.Loaded;

        list.Update(new NeedsYouFeed([NeedsYouRows.Decision()], ""));

        Assert.True(page.IsCleared);
        Assert.False(page.Bar.PrimaryEnabled);
    }
}
