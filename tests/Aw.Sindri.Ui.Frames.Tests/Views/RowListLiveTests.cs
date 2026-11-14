using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Theme;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Rows;
using Aw.Sindri.Ui.Views.Controls.Rows;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// The RowList data contract on a live list (#2978, review of #3189 items 1 to 5, 9 and 12): rows come, go and change while the list is on screen, and the cursor row keeps its
/// container and focus; selection matches by Key; a row follows its item; a chip's colour is the fact's tone; groups fold; the density comes from an ancestor; the list lets go of the
/// view model when it leaves the tree.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class RowListLiveTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(RowListLiveTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    private static LiveGroup Group(string heading, int count, int first = 0) =>
        new(heading, Enumerable.Range(first, count).Select(i => LiveRow.At(i)));

    private static (Window Window, RowList List) Show(IEnumerable groups, double height = 600)
    {
        var list = new RowList { ItemsSource = groups, Columns = RowGallery.Columns };
        var window = new Window { Content = list, Width = 1280, Height = height };
        window.Show();
        Settle(window);
        return (window, list);
    }

    private static List<Row> Rows(RowList list) => [.. list.GetVisualDescendants().OfType<Row>().Where(r => r.IsEffectivelyVisible && r.DataContext is not null).OrderBy(r => r.TranslatePoint(default, list)?.Y ?? 0)];

    private static Row RowOf(RowList list, string key) => Rows(list).First(r => r.DataContext is IRowItem item && item.Key == key);

    private static void Focus(Window window, RowList list, string key)
    {
        Assert.True(RowOf(list, key).Focus(NavigationMethod.Directional));
        Settle(window);
    }

    private static string? FocusedKey(Window window) =>
        (window.FocusManager?.GetFocusedElement() as Visual)?.FindAncestorOfType<Row>(includeSelf: true)?.DataContext is IRowItem item ? item.Key : null;

    private static string TitleOf(Row row) => row.Layout.Title;

    [Fact]
    public async Task Appending_rows_keeps_the_focused_row_in_the_same_container() => await OnUi(() =>
    {
        var group = Group("Needs you", 10);
        var (window, list) = Show(new[] { group });
        Focus(window, list, "k3");
        var row = RowOf(list, "k3");

        foreach (var i in Enumerable.Range(10, 5))
        {
            group.Rows.Add(LiveRow.At(i));
        }

        Settle(window);

        Assert.Equal("k3", FocusedKey(window));
        Assert.Same(row, RowOf(list, "k3"));
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_row_added_above_the_cursor_and_a_row_removed_above_it_leave_focus_on_the_cursor_row() => await OnUi(() =>
    {
        var group = Group("Needs you", 10);
        var (window, list) = Show(new[] { group });
        Focus(window, list, "k5");

        group.Rows.Insert(1, new LiveRow("new", "Fresh"));
        Settle(window);
        Assert.Equal("k5", FocusedKey(window));

        group.Rows.RemoveAt(3);   // k2
        Settle(window);
        Assert.Equal("k5", FocusedKey(window));
        window.Close();
        return true;
    });

    [Fact]
    public async Task Removing_the_focused_row_puts_focus_on_the_row_that_takes_its_place() => await OnUi(() =>
    {
        var group = Group("Needs you", 10);
        var (window, list) = Show(new[] { group });
        Focus(window, list, "k3");

        group.Rows.RemoveAt(3);
        Settle(window);

        Assert.Equal("k4", FocusedKey(window));
        window.Close();
        return true;
    });

    [Fact]
    public async Task Removing_the_last_row_of_a_group_puts_focus_on_the_row_before_it() => await OnUi(() =>
    {
        var group = Group("Needs you", 4);
        var (window, list) = Show(new[] { group });
        Focus(window, list, "k3");

        group.Rows.RemoveAt(3);
        Settle(window);

        Assert.Equal("k2", FocusedKey(window));
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_new_object_with_the_same_key_takes_over_the_row_and_keeps_focus_and_selection() => await OnUi(() =>
    {
        var group = Group("Needs you", 10);
        var (window, list) = Show(new[] { group });
        list.SelectedItem = group.Rows[2];
        Focus(window, list, "k2");

        group.Rows[2] = new LiveRow("k2", "Row 2, polled again");   // a poll hands over fresh objects
        Settle(window);

        var row = RowOf(list, "k2");
        Assert.Equal("Row 2, polled again", TitleOf(row));
        Assert.Contains("sel", row.Classes, StringComparer.Ordinal);
        Assert.Equal("k2", FocusedKey(window));
        window.Close();
        return true;
    });

    [Fact]
    public async Task The_selected_row_is_the_one_with_the_selected_key_whatever_object_holds_it() => await OnUi(() =>
    {
        var group = Group("Needs you", 10);
        var (window, list) = Show(new[] { group });

        list.SelectedItem = new LiveRow("k4", "A different object, the same key");
        Settle(window);

        var selected = Rows(list).Where(r => r.Classes.Contains("sel")).ToList();
        Assert.Equal("k4", Assert.IsType<LiveRow>(Assert.Single(selected).DataContext).Key);
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_row_redraws_its_text_in_place_when_its_item_changes() => await OnUi(() =>
    {
        var group = Group("Needs you", 5);
        var (window, list) = Show(new[] { group });
        var row = RowOf(list, "k1");

        group.Rows[1].Title = "Failed: could not send";
        group.Rows[1].Age = "9m";
        group.Rows[1].Facts = new Dictionary<string, RowFact>(StringComparer.Ordinal) { ["status"] = new(RowFactKind.Chip, "CI red", Tone: RowTone.Danger) };
        Settle(window);

        Assert.Same(row, RowOf(list, "k1"));
        Assert.Equal("Failed: could not send", TitleOf(row));
        Assert.Equal("9m", row.Layout.Age);
        Assert.Equal("CI red", Assert.Single(row.Layout.Chips).Text);
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_chip_takes_its_colour_from_the_fact_tone_and_not_from_its_words() => await OnUi(() =>
    {
        var group = Group("Needs you", 3);
        group.Rows[0].Facts = new Dictionary<string, RowFact>(StringComparer.Ordinal) { ["status"] = new(RowFactKind.Chip, "CI red", Tone: RowTone.Danger) };
        group.Rows[1].Facts = new Dictionary<string, RowFact>(StringComparer.Ordinal) { ["status"] = new(RowFactKind.Chip, "Failed to start a run, in review, waiting") };   // neutral: the words say nothing
        group.Rows[2].Facts = new Dictionary<string, RowFact>(StringComparer.Ordinal) { ["status"] = new(RowFactKind.Chip, "2 of 2 ok", Tone: RowTone.Success) };
        var (window, list) = Show(new[] { group });

        string[] Classes(string key) => [Assert.Single(RowOf(list, key).Layout.Chips).Tone];

        Assert.Equal(["error"], Classes("k0"));
        Assert.Equal(["neutral"], Classes("k1"));
        Assert.Equal(["ok"], Classes("k2"));
        window.Close();
        return true;
    });

    private static List<RowGroupHeader> Headers(RowList list) => [.. list.GetVisualDescendants().OfType<RowGroupHeader>().Where(h => h.Group is not null && h.IsEffectivelyVisible && h.Bounds.Height > 0).OrderBy(h => h.TranslatePoint(default, list)?.Y ?? 0)];

    private static string[] TextsOf(Control c) => [.. c.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty)];

    private static void Click(Window window, Visual target, double x, double y)
    {
        var at = target.TranslatePoint(new Point(x, y), window)!.Value;
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        Settle(window);
    }

    [Fact]
    public async Task Clicking_a_group_header_folds_its_rows_and_keeps_the_count_and_clicking_again_brings_them_back() => await OnUi(() =>
    {
        var needs = Group("Needs you", 8);
        var moving = Group("Moving", 5, first: 100);
        var (window, list) = Show(new[] { needs, moving });
        Assert.Equal(13, Rows(list).Count);

        Click(window, Headers(list)[0], 14, 14);   // the chevron

        Assert.True(needs.IsCollapsed);
        Assert.Equal(5, Rows(list).Count);
        Assert.Contains("8", TextsOf(Headers(list)[0]), StringComparer.Ordinal);   // "Needs you 8", folded
        Assert.Contains("collapsed", Headers(list)[0].Classes, StringComparer.Ordinal);

        Click(window, Headers(list)[0], 60, 14);   // the heading works too

        Assert.False(needs.IsCollapsed);
        Assert.Equal(13, Rows(list).Count);
        window.Close();
        return true;
    });

    private static void Tap(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
        Settle(window);
    }

    [Fact]
    public async Task Left_on_a_row_folds_its_group_and_moves_the_cursor_to_its_header_and_Right_unfolds_it() => await OnUi(() =>
    {
        var needs = Group("Needs you", 4);
        var moving = Group("Moving", 3, first: 100);
        var (window, list) = Show(new[] { needs, moving });
        Focus(window, list, "k2");

        Tap(window, PhysicalKey.ArrowLeft);

        Assert.True(needs.IsCollapsed);
        Assert.Equal(3, Rows(list).Count);
        _ = Assert.IsType<RowGroupHeader>((window.FocusManager?.GetFocusedElement() as Visual)?.FindAncestorOfType<RowGroupHeader>(includeSelf: true));
        Assert.Null(FocusedKey(window));

        Tap(window, PhysicalKey.ArrowRight);

        Assert.False(needs.IsCollapsed);
        Assert.Equal(7, Rows(list).Count);
        window.Close();
        return true;
    });

    [Fact]
    public async Task J_from_a_folded_header_goes_to_the_next_group_and_the_chevron_is_named_by_what_it_does() => await OnUi(() =>
    {
        var needs = Group("Needs you", 4);
        var moving = Group("Moving", 3, first: 100);
        var (window, list) = Show(new[] { needs, moving });
        Focus(window, list, "k0");
        Tap(window, PhysicalKey.ArrowLeft);
        var chevron = Headers(list)[0].GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_Chevron");
        Assert.Equal("Unfold Needs you", AutomationProperties.GetName(chevron));

        Tap(window, PhysicalKey.ArrowDown);

        Assert.Equal("k100", FocusedKey(window));
        needs.IsCollapsed = false;
        Settle(window);
        Assert.Equal("Fold Needs you", AutomationProperties.GetName(chevron));
        window.Close();
        return true;
    });

    [Fact]
    public async Task The_list_is_a_list_that_counts_every_row_and_not_just_the_ones_built() => await OnUi(() =>
    {
        var moving = new LiveGroup("Moving", Enumerable.Range(0, 10_000).Select(i => LiveRow.At(i)));
        var (window, list) = Show(new[] { moving });
        var peer = ControlAutomationPeer.CreatePeerForElement(list);

        Assert.Equal(AutomationControlType.List, peer.GetAutomationControlType());
        Assert.Equal("10000 rows", peer.GetName());

        AutomationProperties.SetName(list, "Inbox");
        Assert.Equal("Inbox, 10000 rows", peer.GetName());

        moving.IsCollapsed = true;
        Settle(window);
        Assert.Equal("Inbox, 0 rows", peer.GetName());
        window.Close();
        return true;
    });

    [Fact]
    public async Task One_row_is_1_row_in_the_list_name_and_the_header_name_and_the_header_says_if_it_is_folded() => await OnUi(() =>
    {
        var moving = Group("Moving", 1);
        var needs = Group("Needs you", 8, first: 100);
        var (window, list) = Show(new[] { moving, needs });
        var peer = ControlAutomationPeer.CreatePeerForElement(list);
        Assert.Equal("9 rows", peer.GetName());
        Assert.Equal("Moving, 1 row, expanded", AutomationProperties.GetName(Headers(list)[0]));
        Assert.Equal("Needs you, 8 rows, expanded", AutomationProperties.GetName(Headers(list)[1]));

        Click(window, Headers(list)[0], 60, 14);

        Assert.Equal("Moving, 1 row, collapsed", AutomationProperties.GetName(Headers(list)[0]));
        Assert.Equal("8 rows", peer.GetName());
        moving.Rows.Clear();
        moving.IsCollapsed = false;
        Settle(window);
        Assert.Equal("Moving, 0 rows, expanded", AutomationProperties.GetName(Headers(list)[0]));
        window.Close();
        return true;
    });

    [Fact]
    public async Task Esc_still_reaches_the_page_after_the_last_row_leaves_under_an_open_peek() => await OnUi(() =>
    {
        var group = Group("Needs you", 2);
        var (window, list) = Show(new[] { group });
        list.IsPeekOpen = true;
        var backs = new List<object?>();
        list.BackRequested += (_, e) => backs.Add(e.Item);
        Focus(window, list, "k1");

        group.Rows.Clear();
        Settle(window);
        Tap(window, PhysicalKey.Escape);

        Assert.Equal([null], backs);   // one Back, with no item: the page can close the peek
        window.Close();
        return true;
    });

    [Fact]
    public async Task Dot_asks_for_the_cursor_rows_actions_and_the_window_never_sees_the_key() => await OnUi(() =>
    {
        var group = Group("Needs you", 4);
        var (window, list) = Show(new[] { group });
        var asked = new List<object?>();
        list.ActionsRequested += (_, e) => asked.Add(e.Item);
        var shellSaw = false;
        window.AddHandler(InputElement.KeyDownEvent, (_, _) => shellSaw = true, RoutingStrategies.Bubble);
        Focus(window, list, "k2");

        Tap(window, PhysicalKey.Period);

        Assert.Same(group.Rows[2], Assert.Single(asked));
        Assert.False(shellSaw);
        window.Close();
        return true;
    });

    [Fact]
    public async Task The_cursor_ring_is_2_px_inside_the_row_and_does_not_change_its_height() => await OnUi(() =>
    {
        var (window, list) = Show(new[] { Group("Needs you", 4) });
        var row = RowOf(list, "k1");
        var resting = row.Bounds.Height;

        Focus(window, list, "k1");

        Assert.True(row.Layout.Ring);
        Assert.Equal((2.0, 4.0), (Row.RingStroke, Row.RingCorner));
        Assert.Equal(resting, row.Bounds.Height, 0.01);
        window.Close();
        return true;
    });

    [Fact]
    public async Task When_the_peek_closes_focus_goes_back_to_the_row_that_was_peeked() => await OnUi(() =>
    {
        var group = Group("Needs you", 5);
        var list = new RowList { ItemsSource = new[] { group }, Columns = RowGallery.Columns, IsPeekOpen = true, SelectedItem = group.Rows[3] };
        var peek = new PeekPane();
        var page = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(peek, 1);
        page.Children.Add(list);
        page.Children.Add(peek);
        var window = new Window { Content = page, Width = 1280, Height = 600 };
        window.Show();
        Settle(window);
        var button = peek.GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_Close");
        Assert.True(button.Focus(NavigationMethod.Pointer));

        peek.IsVisible = false;   // the page hides the pane and closes the peek
        list.IsPeekOpen = false;
        Settle(window);

        Assert.Equal("k3", FocusedKey(window));
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_long_ref_is_cut_again_to_fit_its_column_when_the_text_scale_goes_from_100_to_130_percent_live() => await OnUi(() =>
    {
        var resources = Application.Current!.Resources;
        var group = new LiveGroup("Needs you", [LiveRow.At(0)]);
        group.Rows[0].Facts = new Dictionary<string, RowFact>(StringComparer.Ordinal) { ["ref"] = new(RowFactKind.Ref, RowGallery.LongRef) };
        var (window, list) = Show(new[] { group });
        RowCell Cell() => RowOf(list, "k0").Layout.Cells.Single(c => string.Equals(c.Key, "ref", StringComparison.Ordinal));
        double Fitted() => Cell().TextWidth;

        var at100 = Fitted();
        try
        {
            TextScale.Apply(resources, 1.3);
            Settle(window);
            var width = Cell().Bounds.Width;

            Assert.True(Fitted() <= width + 0.5, string.Create(CultureInfo.InvariantCulture, $"ref is {Fitted():0.0} px wide in a {width:0.0} px column at 130% (it was {at100:0.0} at 100%)"));
        }
        finally
        {
            TextScale.Apply(resources, 1.0);
        }

        window.Close();
        return true;
    });

    [Fact]
    public async Task A_group_set_to_collapsed_by_the_page_starts_folded_and_rows_added_to_it_stay_hidden() => await OnUi(() =>
    {
        var moving = Group("Moving", 5);
        moving.IsCollapsed = true;
        var (window, list) = Show(new[] { moving });
        Assert.Empty(Rows(list));

        moving.Rows.Add(new LiveRow("k99", "Late"));
        Settle(window);

        Assert.Empty(Rows(list));
        Assert.Contains("6", TextsOf(Headers(list)[0]), StringComparer.Ordinal);
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_group_header_follows_its_group_heading_summary_and_count() => await OnUi(() =>
    {
        var group = Group("Needs you", 3);
        var (window, list) = Show(new[] { group });

        group.Heading = "Needs a reply";
        group.Summary = "oldest 3d";
        group.Rows.Add(new LiveRow("k9", "Row 9"));
        Settle(window);

        var texts = TextsOf(Headers(list)[0]);
        Assert.Contains("Needs a reply", texts, StringComparer.Ordinal);
        Assert.Contains("oldest 3d", texts, StringComparer.Ordinal);
        Assert.Contains("4", texts, StringComparer.Ordinal);
        window.Close();
        return true;
    });

    [Fact]
    public async Task The_sticky_header_draws_the_ring_while_the_focused_header_it_covers_is_under_it() => await OnUi(() =>
    {
        var first = Group("First", 14);
        var second = Group("Second", 14, first: 100);
        var (window, list) = Show(new[] { first, second }, height: 300);
        var real = list.GetVisualDescendants().OfType<RowGroupHeader>().First(h => h.Name != "PART_Sticky" && ReferenceEquals(h.Group, first));
        Assert.True(real.Focus(NavigationMethod.Directional));

        list.ScrollTo(10);   // the first header sits 10 px under the sticky copy of the same group
        Settle(window);
        var sticky = list.GetVisualDescendants().OfType<RowGroupHeader>().First(h => h.Name == "PART_Sticky");
        Assert.Same(first, list.StickyGroup);
        Assert.Equal(new Thickness(2), sticky.BorderThickness);
        Assert.True(sticky.TryFindResource("Brush.PrimaryHover", sticky.ActualThemeVariant, out var expected));
        Assert.Equal(Assert.IsType<ISolidColorBrush>(expected, exactMatch: false).Color, Assert.IsType<ISolidColorBrush>(sticky.BorderBrush, exactMatch: false).Color);
        window.Close();
        return true;
    });

    [Fact]
    public async Task Comfortable_on_an_ancestor_sets_the_row_pitch_and_the_sticky_header_swaps_at_the_right_scroll_offset() => await OnUi(() =>
    {
        var first = Group("First", 14);
        var second = Group("Second", 14, first: 100);
        var (window, list) = Show(new[] { first, second }, height: 300);
        window.Classes.Set("comfortable", value: true);   // the grammar puts it on .app, never on the list
        Settle(window);
        const double SecondTop = 8 + 28 + (14 * 40) + 8;

        list.ScrollTo(SecondTop - 60);   // 48 px before the second header; a list that assumed 32 px rows would already think it is there
        Settle(window);
        Assert.Same(first, list.StickyGroup);

        list.ScrollTo(SecondTop + 1);
        Settle(window);
        Assert.Same(second, list.StickyGroup);
        Assert.Equal(40, list.RowHeight, 0.01);
        window.Close();
        return true;
    });

    [Fact]
    public async Task Turning_comfortable_on_and_off_changes_the_row_height_the_list_uses() => await OnUi(() =>
    {
        var (window, list) = Show(new[] { Group("First", 5) });
        Assert.Equal(32, list.RowHeight, 0.01);

        window.Classes.Set("comfortable", value: true);
        Assert.Equal(40, list.RowHeight, 0.01);

        window.Classes.Set("comfortable", value: false);
        Assert.Equal(32, list.RowHeight, 0.01);
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_list_that_leaves_the_tree_lets_go_of_the_view_model_and_takes_it_up_again_on_return() => await OnUi(() =>
    {
        var rows = new CountingRows(3);
        var group = new SourceGroup(rows);
        var groups = new CountingGroups(group);
        var list = new RowList { ItemsSource = groups, Columns = RowGallery.Columns };
        var window = new Window { Content = list, Width = 1280, Height = 600 };
        window.Show();
        Settle(window);
        Assert.All([groups.Subscribers, rows.Subscribers, group.Subscribers], n => Assert.True(n > 0, "the list listens while it is on screen"));

        window.Content = null;
        Settle(window);
        Assert.Equal((0, 0, 0), (groups.Subscribers, rows.Subscribers, group.Subscribers));

        rows.Add(new LiveRow("late", "Late"));   // while away: the list must not hear it
        window.Content = list;
        Settle(window);
        Assert.All([groups.Subscribers, rows.Subscribers, group.Subscribers], n => Assert.True(n > 0, "the list listens while it is on screen"));
        Assert.Equal(4, Rows(list).Count);   // and sees it on return
        window.Close();
        return true;
    });

    /// <summary>A rows collection that counts who listens.</summary>
    private sealed class CountingRows(int count) : IEnumerable, INotifyCollectionChanged
    {
        private readonly List<LiveRow> _rows = [.. Enumerable.Range(0, count).Select(i => LiveRow.At(i))];
        private NotifyCollectionChangedEventHandler? _handler;

        public int Subscribers => _handler?.GetInvocationList().Length ?? 0;

        public event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add => _handler += value;
            remove => _handler -= value;
        }

        public void Add(LiveRow row)
        {
            _rows.Add(row);
            _handler?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, row, _rows.Count - 1));
        }

        public IEnumerator GetEnumerator() => _rows.GetEnumerator();
    }

    private sealed class SourceGroup(CountingRows rows) : IRowGroupHeading
    {
        private System.ComponentModel.PropertyChangedEventHandler? _handler;

        public int Subscribers => _handler?.GetInvocationList().Length ?? 0;

        public IEnumerable Items => rows;

        public string Heading => "Counted";

        public string? Summary => null;

        public bool IsCollapsed { get; set; }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged
        {
            add => _handler += value;
            remove => _handler -= value;
        }
    }

    private sealed class CountingGroups(SourceGroup group) : IEnumerable<IRowGroup>, INotifyCollectionChanged
    {
        private NotifyCollectionChangedEventHandler? _handler;

        public int Subscribers => _handler?.GetInvocationList().Length ?? 0;

        public event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add => _handler += value;
            remove => _handler -= value;
        }

        public IEnumerator<IRowGroup> GetEnumerator()
        {
            yield return group;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
