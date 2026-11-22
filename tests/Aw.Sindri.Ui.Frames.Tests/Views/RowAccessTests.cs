using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Rows;
using Aw.Sindri.Ui.Views.Controls;
using Aw.Sindri.Ui.Views.Controls.Rows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The RowList's accessibility, tab stops, contrast and pointer path (#2978, review of #3189 items 6, 7, 8, 10, 14, 15).</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class RowAccessTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(RowAccessTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static (Window Window, RowList List) Show(IReadOnlyList<IRowGroupHeading> groups, double height = 600)
    {
        var list = new RowList { ItemsSource = groups, Columns = RowGallery.Columns };
        var window = new Window { Content = list, Width = 1280, Height = height };
        window.Show();
        Settle(window);
        return (window, list);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static Color Brush(string key) => Application.Current!.FindResource(key) is ISolidColorBrush b ? b.Color : default;

    private static List<Row> Rows(Visual root) => [.. root.GetVisualDescendants().OfType<Row>().Where(r => r.IsEffectivelyVisible && r.Bounds.Height > 0)];

    // Item 8: the sub-line and the peek label use the ink that holds 4.5:1 on the selected row's fill.
    [Fact]
    public async Task The_sub_line_and_the_peek_label_use_InkSubtle() => await OnUi(() =>
    {
        var (window, list) = Show([new OneGroup("Needs you", [new SubRow()])]);
        var peek = new PeekPane { Fields = [new PeekField("Owner", "Maya")] };
        var host = new Window { Content = peek, Width = 500, Height = 300 };
        host.Show();
        Settle(host);

        var sub = Rows(list)[0].Layout;
        var label = peek.GetVisualDescendants().OfType<TextBlock>().First(t => t.Classes.Contains("peek-label"));

        Assert.Equal(Brush("Brush.InkSubtle"), Assert.IsType<ISolidColorBrush>(sub.SubInk, exactMatch: false).Color);
        Assert.Equal(Brush("Brush.InkSubtle"), Assert.IsType<ISolidColorBrush>(label.Foreground, exactMatch: false).Color);
        window.Close();
        host.Close();
        return true;
    });

    // Item 6: a screen reader gets a name for the row, the glyph and the peek buttons, and skips the sticky clone.
    [Fact]
    public async Task A_row_is_named_from_its_state_title_facts_who_and_age() => await OnUi(() =>
    {
        var (window, list) = Show([RowGallery.Group("Needs you", 3)]);

        var row = Rows(list)[1];
        var name = AutomationProperties.GetName(row);

        Assert.StartsWith("in review, Row 1, In review, 3/8, +120 -4, acme/exports, Maya", name, StringComparison.Ordinal);
        Assert.EndsWith("waits on you, 12m", name, StringComparison.Ordinal);
        window.Close();
        return true;
    });

    [Fact]
    public async Task The_peek_buttons_have_accessible_names_and_the_sticky_header_is_raw() => await OnUi(() =>
    {
        var (window, list) = Show(RowGallery.Many(3, 20));
        var peek = new PeekPane();
        var host = new Window { Content = peek, Width = 500, Height = 300 };
        host.Show();
        Settle(host);

        var names = peek.GetVisualDescendants().OfType<Button>().Select(AutomationProperties.GetName).ToList();
        var sticky = list.GetVisualDescendants().OfType<RowGroupHeader>().Single(h => h.Name == "PART_Sticky");

        Assert.Equal(["Open full", "Close"], names);
        Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(sticky));
        window.Close();
        host.Close();
        return true;
    });

    // Item 7: the list is one tab stop; the row buttons are not stops.
    [Fact]
    public async Task The_list_is_one_tab_stop() => await OnUi(() =>
    {
        var (window, list) = Show([RowGallery.Group("Needs you", 5, 0, i => RowGallery.Row(i, action: new CountingCommand()))]);

        Assert.Equal(KeyboardNavigationMode.Once, KeyboardNavigation.GetTabNavigation(list));
        window.Close();
        return true;
    });

    private static void Tap(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    private static List<(string Name, object? Item)> Listen(RowList list)
    {
        var seen = new List<(string, object?)>();
        list.PeekRequested += (_, e) => seen.Add(("peek", e.Item));
        list.OpenRequested += (_, e) => seen.Add(("open", e.Item));
        list.BackRequested += (_, e) => seen.Add(("back", e.Item));
        return seen;
    }

    // Item 10: k never leaves the cursor row under the sticky header.
    [Fact]
    public async Task K_across_a_group_boundary_keeps_the_cursor_row_below_the_sticky_header() => await OnUi(() =>
    {
        var (window, list) = Show(RowGallery.Many(3, 14));
        list.ScrollTo((14 * 32d) + 28 + (6 * 32));   // inside the second group
        Settle(window);
        var first = Rows(list).First(r => r.TranslatePoint(default, list)?.Y >= 28);
        Assert.True(first.Focus(NavigationMethod.Directional));

        for (var i = 0; i < 20; i++)
        {
            Tap(window, PhysicalKey.K);
            var focused = (window.FocusManager?.GetFocusedElement() as Visual)?.FindAncestorOfType<Row>(includeSelf: true);
            Assert.NotNull(focused);
            var top = focused.TranslatePoint(default, list)?.Y ?? double.NaN;
            Assert.True(list.StickyGroup is null || top >= 28 - 0.5, string.Create(CultureInfo.InvariantCulture, $"after {i + 1} presses of k the row is at {top} under the sticky header"));
        }

        window.Close();
        return true;
    });

    // Item 14: Esc and Space reach the page with no cursor row.
    [Fact]
    public async Task Esc_and_Space_work_on_an_empty_list() => await OnUi(() =>
    {
        var (window, list) = Show([]);
        var seen = Listen(list);
        Assert.True(list.Focus());

        Tap(window, PhysicalKey.Escape);
        Tap(window, PhysicalKey.Space);

        Assert.Equal("back,peek", string.Join(',', seen.Select(x => x.Name)));
        Assert.All(seen, x => Assert.Null(x.Item));
        window.Close();
        return true;
    });

    [Fact]
    public async Task J_from_the_list_itself_lands_on_the_first_row() => await OnUi(() =>
    {
        var (window, list) = Show([RowGallery.Group("Needs you", 5)]);
        Assert.True(list.Focus());

        Tap(window, PhysicalKey.J);

        var id = (window.FocusManager?.GetFocusedElement() as Visual)?.FindAncestorOfType<Row>(includeSelf: true)?.DataContext as IRowItem;
        Assert.Equal("r-0000", id?.Id);
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_Space_down_whose_key_up_went_elsewhere_does_not_make_the_next_tap_a_hold() => await OnUi(() =>
    {
        var clock = new FakeTimeProvider();
        RowKeys.Clock = clock;
        try
        {
            var (window, list) = Show([RowGallery.Group("Needs you", 5)]);
            var other = new Button();
            var host = new Window { Content = other, Width = 100, Height = 100 };
            host.Show();
            var seen = new List<(bool Hold, bool Released)>();
            list.AddHandler(RowKeys.PeekRequestedEvent, (_, e) => seen.Add((e.Hold, e.Released)));
            Assert.True(Rows(list)[0].Focus(NavigationMethod.Directional));

            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.True(other.Focus());   // focus leaves the list before the key comes up
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromSeconds(2));
            Assert.True(Rows(list)[0].Focus(NavigationMethod.Directional));
            Tap(window, PhysicalKey.Space);

            Assert.Equal([(false, false)], seen);
            window.Close();
            host.Close();
            return true;
        }
        finally
        {
            RowKeys.Clock = TimeProvider.System;
        }
    });

    // #3313: the list's name carries its row count, so a changed count has to tell the screen reader.
    [Fact]
    public async Task A_changed_row_count_raises_one_name_changed_event_that_carries_the_new_name() => await OnUi(() =>
    {
        var group = new LiveGroup("Moving", Enumerable.Range(0, 3).Select(i => LiveRow.At(i)));
        var (window, list) = Show([group]);
        var peer = ControlAutomationPeer.CreatePeerForElement(list);
        var events = new List<(string? Old, string? New)>();
        peer.PropertyChanged += (_, e) =>
        {
            if (e.Property == AutomationElementIdentifiers.NameProperty)
            {
                events.Add((e.OldValue as string, e.NewValue as string));
            }
        };

        group.Rows.Add(LiveRow.At(3));
        Settle(window);

        Assert.Equal([("3 rows", "4 rows")], events);

        events.Clear();
        group.IsCollapsed = true;   // folding changes the count too
        Settle(window);

        Assert.Equal([("4 rows", "0 rows")], events);
        Assert.Equal("0 rows", peer.GetName());
        window.Close();
        return true;
    });

    // Item 15: the mouse path.
    [Fact]
    public async Task A_click_puts_the_cursor_on_the_row_and_a_double_click_opens_it() => await OnUi(() =>
    {
        var groups = new[] { RowGallery.Group("Needs you", 6) };
        var (window, list) = Show(groups);
        var seen = Listen(list);
        var row = Rows(list)[2];
        var point = row.TranslatePoint(new Point(300, 10), window) ?? default;

        window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.True(row.IsKeyboardFocusWithin);
        Assert.Empty(seen);

        window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([("open", (object?)groups[0].Rows[2])], seen);   // exactly one open: the second click of a double-click doesn't toggle the peek too
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_double_click_on_the_row_that_has_the_cursor_peeks_once_for_the_first_click_and_then_opens() => await OnUi(() =>
    {
        var groups = new[] { RowGallery.Group("Needs you", 6) };
        var (window, list) = Show(groups);
        var seen = Listen(list);
        var row = Rows(list)[2];
        Assert.True(row.Focus(NavigationMethod.Directional));
        var point = row.TranslatePoint(new Point(300, 10), window) ?? default;

        for (var click = 0; click < 2; click++)
        {
            window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal([("peek", (object?)groups[0].Rows[2]), ("open", groups[0].Rows[2])], seen);
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_click_on_the_row_that_has_the_cursor_toggles_the_peek() => await OnUi(() =>
    {
        var groups = new[] { RowGallery.Group("Needs you", 6) };
        var (window, list) = Show(groups);
        var seen = Listen(list);
        var row = Rows(list)[2];
        Assert.True(row.Focus(NavigationMethod.Directional));
        var point = row.TranslatePoint(new Point(300, 10), window) ?? default;

        window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([("peek", (object?)groups[0].Rows[2])], seen);
        window.Close();
        return true;
    });

    private sealed class OneGroup(string heading, IReadOnlyList<IRowItem> rows) : IRowGroupHeading
    {
        public string Heading { get; } = heading;

        public string? Summary => null;

        public System.Collections.IEnumerable Items { get; } = rows;

        public bool IsCollapsed { get; set; }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }
    }

    private sealed record SubRow() : IRowItem
    {
        public string Id => "r-sub";

        public string Key => "test:pr:sub";

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        public string Title => "With a sub line";

        public string? SubLine => "the sub line";

        public string Kind => "pr";

        public RowStateClass StateClass => RowStateClass.Waiting;

        public string? StateKey => "in-review";

        public bool Unread => false;

        public IReadOnlyDictionary<string, RowFact> Facts { get; } = new Dictionary<string, RowFact>(StringComparer.Ordinal);

        public string Age => "1m";

        public RowWaitsOn? WaitsOn => null;

        public IReadOnlyList<RowAction> Actions { get; } = [];
    }
}
