using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Rows;
using Aw.Sindri.Ui.Views.Controls;
using Aw.Sindri.Ui.Views.Controls.Rows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// The RowList's keys on top of RowKeys (#2978): j and k move, Space taps toggle the peek and holds show it until the key comes up, Enter opens, Esc steps back unless a sheet is open.
/// A plain items control with RowKeys keeps today's behaviour. A hold is a key down, a repeat after 250 ms or more and a key up; whether the list raises the hold on the repeat or on the key up,
/// the events are the same two.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class RowKeysPeekTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(RowKeysPeekTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private sealed record Host(Window Window, RowList List, FakeTimeProvider Clock, CountingCommand Action, List<(string Name, int Index, bool Hold, bool Released)> Seen) : IDisposable
    {
        public void Dispose()
        {
            RowKeys.Clock = TimeProvider.System;
            RowKeys.IsSheetOpen = null;
            Window.Close();
        }
    }

    private static Host Show()
    {
        var clock = new FakeTimeProvider();
        RowKeys.Clock = clock;
        var action = new CountingCommand();
        var group = RowGallery.Group("Needs you", 30, 0, i => RowGallery.Row(i, action: action));
        var list = new RowList { ItemsSource = new[] { group }, Columns = RowGallery.Columns };
        var seen = new List<(string, int, bool, bool)>();
        list.AddHandler(RowKeys.PeekRequestedEvent, (_, e) => seen.Add(("peek", e.Index, e.Hold, e.Released)));
        list.AddHandler(RowKeys.OpenRequestedEvent, (_, e) => seen.Add(("open", e.Index, e.Hold, e.Released)));
        list.AddHandler(RowKeys.BackRequestedEvent, (_, e) => seen.Add(("back", e.Index, e.Hold, e.Released)));
        var window = new Window { Content = list, Width = 1280, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return new Host(window, list, clock, action, seen);
    }

    private static void Down(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Up(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyReleaseQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Tap(Window window, PhysicalKey key)
    {
        Down(window, key);
        Up(window, key);
    }

    private static Row FirstRow(RowList list) => list.GetVisualDescendants().OfType<Row>().First(r => r.IsEffectivelyVisible);

    private static string FocusedId(Window window) =>
        (window.FocusManager?.GetFocusedElement() as Visual)?.FindAncestorOfType<Row>(includeSelf: true)?.DataContext is IRowItem item ? item.Id : "outside the rows";

    private static void FocusFirst(Host host) => Assert.True(FirstRow(host.List).Focus(NavigationMethod.Directional));

    [Fact]
    public async Task J_moves_down_and_k_moves_up() => await OnUi(() =>
    {
        using var host = Show();
        FocusFirst(host);
        Assert.Equal("r-0000", FocusedId(host.Window));

        Tap(host.Window, PhysicalKey.J);
        Tap(host.Window, PhysicalKey.J);
        Assert.Equal("r-0002", FocusedId(host.Window));

        Tap(host.Window, PhysicalKey.K);
        Assert.Equal("r-0001", FocusedId(host.Window));
        Assert.Empty(host.Seen);
        return true;
    });

    [Fact]
    public async Task A_quick_Space_tap_raises_one_peek_toggle() => await OnUi(() =>
    {
        using var host = Show();
        FocusFirst(host);

        Down(host.Window, PhysicalKey.Space);
        host.Clock.Advance(TimeSpan.FromMilliseconds(100));
        Up(host.Window, PhysicalKey.Space);

        var peek = Assert.Single(host.Seen);
        Assert.Equal(("peek", false, false), (peek.Name, peek.Hold, peek.Released));
        return true;
    });

    [Fact]
    public async Task A_held_Space_raises_hold_and_then_released_on_key_up() => await OnUi(() =>
    {
        using var host = Show();
        FocusFirst(host);

        Down(host.Window, PhysicalKey.Space);
        host.Clock.Advance(TimeSpan.FromMilliseconds(300));
        Down(host.Window, PhysicalKey.Space);   // the key repeat the OS sends while it is held
        Up(host.Window, PhysicalKey.Space);

        Assert.Equal([("peek", true, false), ("peek", true, true)], host.Seen.Select(s => (s.Name, s.Hold, s.Released)).ToList());
        return true;
    });

    [Fact]
    public async Task Key_repeat_inside_the_tap_time_is_not_a_second_tap() => await OnUi(() =>
    {
        using var host = Show();
        FocusFirst(host);

        Down(host.Window, PhysicalKey.Space);
        host.Clock.Advance(TimeSpan.FromMilliseconds(50));
        Down(host.Window, PhysicalKey.Space);
        Down(host.Window, PhysicalKey.Space);
        host.Clock.Advance(TimeSpan.FromMilliseconds(50));
        Up(host.Window, PhysicalKey.Space);

        _ = Assert.Single(host.Seen);
        return true;
    });

    [Fact]
    public async Task Enter_raises_open_and_Esc_raises_back() => await OnUi(() =>
    {
        using var host = Show();
        FocusFirst(host);

        Tap(host.Window, PhysicalKey.Enter);
        Tap(host.Window, PhysicalKey.Escape);

        Assert.Equal(["open", "back"], host.Seen.Select(s => s.Name).ToList());
        return true;
    });

    [Fact]
    public async Task Esc_raises_back_only_when_no_sheet_is_open() => await OnUi(() =>
    {
        using var host = Show();
        FocusFirst(host);
        RowKeys.IsSheetOpen = static () => true;

        Tap(host.Window, PhysicalKey.Escape);
        Assert.Empty(host.Seen);

        RowKeys.IsSheetOpen = null;
        Tap(host.Window, PhysicalKey.Escape);
        Assert.Equal(["back"], host.Seen.Select(s => s.Name).ToList());
        return true;
    });

    [Fact]
    public async Task No_key_is_taken_from_a_text_box_or_with_a_modifier() => await OnUi(() =>
    {
        using var host = Show();
        FocusFirst(host);

        var box = new TextBox();   // not in the tree: only the key's source matters to the rule
        host.List.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space, Source = box });
        host.List.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = box });
        host.List.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.J, Source = box });
        Down(host.Window, PhysicalKey.Space, RawInputModifiers.Control);
        Up(host.Window, PhysicalKey.Space, RawInputModifiers.Control);
        Down(host.Window, PhysicalKey.J, RawInputModifiers.Control);
        Assert.Empty(host.Seen);
        Assert.Equal("r-0000", FocusedId(host.Window));

        Tap(host.Window, PhysicalKey.Space);   // the control: a plain Space does work
        _ = Assert.Single(host.Seen);
        return true;
    });

    [Fact]
    public async Task Space_on_a_focused_button_in_a_row_presses_the_button_and_does_not_peek() => await OnUi(() =>
    {
        using var host = Show();
        FocusFirst(host);
        Dispatcher.UIThread.RunJobs();
        var button = FirstRow(host.List).GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible);
        Assert.True(button.Focus(NavigationMethod.Tab));

        Tap(host.Window, PhysicalKey.Space);

        Assert.Equal(1, host.Action.Runs);
        Assert.Empty(host.Seen);
        return true;
    });

    [Fact]
    public async Task A_plain_items_control_with_RowKeys_leaves_Space_and_Enter_alone() => await OnUi(() =>
    {
        var list = new ItemsControl
        {
            ItemsSource = Enumerable.Range(0, 50).Select(i => "Row " + i.ToString(CultureInfo.InvariantCulture)).ToList(),
            ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel()),
            ItemTemplate = new FuncDataTemplate<string>((text, _) => new Border { Focusable = true, Height = 24, Child = new TextBlock { Text = text } }),
        };
        RowKeys.SetEnabled(list, value: true);
        var events = 0;
        var handled = false;
        list.AddHandler(RowKeys.PeekRequestedEvent, (_, _) => events++);
        list.AddHandler(RowKeys.OpenRequestedEvent, (_, _) => events++);
        list.AddHandler(InputElement.KeyDownEvent, (_, e) => handled |= e.Handled, RoutingStrategies.Bubble, handledEventsToo: true);
        var window = new Window { Content = new ScrollViewer { Content = list }, Width = 400, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        RowKeys.FocusRow(list, 0);

        Tap(window, PhysicalKey.Space);
        Tap(window, PhysicalKey.Enter);

        Assert.Equal(0, events);
        Assert.False(handled, "RowKeys took Space or Enter on a list that is not a RowList");
        window.Close();
        return true;
    });
}
