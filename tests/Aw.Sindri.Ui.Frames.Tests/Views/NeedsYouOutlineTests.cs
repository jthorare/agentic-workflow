using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.NeedsYou;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The reduced-motion outline never hides the focus ring or the selection bar (review run 1, K2; UI spec §11.1).</summary>
[Lane(TestLane.Integration)]
public sealed class NeedsYouOutlineTests : FrameHost
{
    private static readonly Color _ring = Color.FromRgb(0x82, 0x8f, 0xff);

    private static Border Frame(NeedsYouRow row) => row.GetVisualDescendants().OfType<Border>().First(b => string.Equals(b.Name, "RowFrame", StringComparison.Ordinal));

    private static List<NeedsYouRow> Rows(Window window) =>
        [.. Named<Control>(window, "NeedsYouPanel").GetVisualDescendants().OfType<NeedsYouRow>().Where(r => r.IsEffectivelyVisible)];

    private static Color Brush(string key) => Application.Current!.FindResource(key) is ISolidColorBrush b ? b.Color : default;

    private static Color BrushOf(Border border) => (border.BorderBrush as ISolidColorBrush)?.Color ?? Colors.Transparent;

    // Where the row's content starts inside the frame: border plus padding, per side.
    private static Thickness Inset(Border border) => new(border.BorderThickness.Left + border.Padding.Left, border.BorderThickness.Top + border.Padding.Top, border.BorderThickness.Right + border.Padding.Right, border.BorderThickness.Bottom + border.Padding.Bottom);

    [Fact]
    public Task A_focused_outlined_row_shows_the_focus_ring_and_its_content_does_not_move() => OnUi(async () =>
    {
        Motion.Set(reduce: true);
        using var instant = InstantMotion.Start();
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 0);
        var row = Rows(window).First(r => r.DataContext is ViewModels.NeedsYou.NeedsYouRowViewModel { HasOutline: true });
        var frame = Frame(row);
        Assert.Contains("outlined", frame.Classes, StringComparer.Ordinal);
        var before = Inset(frame);

        Assert.True(frame.Focus(NavigationMethod.Tab));
        Settle(window);

        Assert.Equal(_ring, BrushOf(frame));
        Assert.Equal(2, frame.BorderThickness.Left);
        Assert.Equal(before, Inset(frame));
        Frame(window, "needs-you-outlined-focused");
        Assert.NotNull(shell);
    });

    [Fact]
    public Task An_outlined_row_keeps_its_border_padding_and_height_under_the_pointer_and_while_pressed() => OnUi(async () =>
    {
        Motion.Set(reduce: true);
        using var instant = InstantMotion.Start();
        var (window, _) = await Connected(s => s.NeedsYou.Count > 0);
        var frame = Frame(Rows(window).First(r => r.DataContext is ViewModels.NeedsYou.NeedsYouRowViewModel { HasOutline: true }));
        // Where the content is laid out, so a shift shows even if border and padding traded off against each other. Layout
        // bounds, not TranslatePoint: a press moves nothing.
        double ContentTop() => frame.Bounds.Y + frame.Child!.Bounds.Y;
        var inset = Inset(frame);
        var height = frame.Bounds.Height;
        var top = ContentTop();
        var point = frame.TranslatePoint(new Point(4, 4), window)!.Value;
        Frame(window, "outlined-rest", "gh2535-");

        window.MouseMove(point, RawInputModifiers.None);
        Settle(window);
        Assert.True(frame.IsPointerOver);
        Assert.Equal((inset, height, top), (Inset(frame), frame.Bounds.Height, ContentTop()));
        Frame(window, "outlined-hover", "gh2535-");

        window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        Settle(window);
        Assert.Contains(":pressed", frame.Classes, StringComparer.Ordinal);
        Assert.Equal((inset, height, top), (Inset(frame), frame.Bounds.Height, ContentTop()));
        window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
    });

    // #2780: under the pointer an outlined row keeps its neutral border, and under the pointer and a press it takes the hover fill, and the pressed fill on a press (no amber, one accent).
    [Fact]
    public Task An_outlined_row_keeps_its_neutral_border_under_the_pointer_and_the_pressed_fill_while_pressed() => OnUi(async () =>
    {
        Motion.Set(reduce: true);
        using var instant = InstantMotion.Start();
        var (window, _) = await Connected(s => s.NeedsYou.Count > 0);
        var frame = Frame(Rows(window).First(r => r.DataContext is ViewModels.NeedsYou.NeedsYouRowViewModel { HasOutline: true }));
        var point = frame.TranslatePoint(new Point(4, 4), window)!.Value;
        Frame(window, "outlined-amber-rest", "gh2611-");
        var rest = ((ISolidColorBrush)frame.Background!).Color;
        Assert.Equal(Brush("Brush.Clickable.Rest"), rest);

        window.MouseMove(point, RawInputModifiers.None);
        Settle(window);
        Assert.True(frame.IsPointerOver);
        Frame(window, "outlined-amber-hover", "gh2611-");
        Assert.NotEqual(rest, Brush("Brush.Clickable.Hover"));   // rest, hover and press are three different fills
        Assert.NotEqual(Brush("Brush.Clickable.Hover"), Brush("Brush.Clickable.Pressed"));
        Assert.Equal((Brush("Brush.Clickable.Hover"), Brush("Brush.InkSubtle")), (((ISolidColorBrush)frame.Background!).Color, ((ISolidColorBrush)frame.BorderBrush!).Color));

        window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        Settle(window);
        Assert.Contains(":pressed", frame.Classes, StringComparer.Ordinal);
        // A press selects the row, and a selected outlined row draws the selection bar (A_selected_outlined_row_keeps_the_selection_bar_colour).
        Assert.Contains("selected", frame.Classes, StringComparer.Ordinal);
        Assert.Equal((Brush("Brush.Clickable.Pressed"), Brush("Brush.Primary")), (((ISolidColorBrush)frame.Background!).Color, ((ISolidColorBrush)frame.BorderBrush!).Color));
        window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
    });

    [Fact]
    public Task A_selected_outlined_row_keeps_the_selection_bar_colour() => OnUi(async () =>
    {
        Motion.Set(reduce: true);
        using var instant = InstantMotion.Start();
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 0);
        var row = Rows(window).First(r => r.DataContext is ViewModels.NeedsYou.NeedsYouRowViewModel { HasOutline: true });
        var frame = Frame(row);
        var id = ((ViewModels.NeedsYou.NeedsYouRowViewModel)row.DataContext!).Id;

        shell.SelectNeedsYouRow(id);
        Settle(window);

        Assert.Contains("selected", frame.Classes, StringComparer.Ordinal);
        // Focus may have followed the selection (the ring) or not (the bar): either way the attention outline is not what shows.
        Assert.Contains(BrushOf(frame), new[] { Brush("Brush.Primary"), Brush("Brush.PrimaryHover") });
    });

    [Fact]
    public Task A_selected_outlined_row_differs_from_the_focus_ring_by_shape_and_its_content_does_not_move() => OnUi(async () =>
    {
        Motion.Set(reduce: true);
        using var instant = InstantMotion.Start();
        var (window, shell) = await Connected(s => s.NeedsYou.Count > 0);
        var row = Rows(window).First(r => r.DataContext is ViewModels.NeedsYou.NeedsYouRowViewModel { HasOutline: true });
        var frame = Frame(row);
        var before = Inset(frame);

        shell.SelectNeedsYouRow(((ViewModels.NeedsYou.NeedsYouRowViewModel)row.DataContext!).Id);
        Settle(window);
        var elsewhere = window.GetVisualDescendants().OfType<Button>().First(b => b.Focusable && b.IsEffectivelyVisible && !row.IsVisualAncestorOf(b));
        Assert.True(elsewhere.Focus(NavigationMethod.Pointer));   // focus sits on another control
        Settle(window);

        Assert.Contains("selected", frame.Classes, StringComparer.Ordinal);
        Assert.False(frame.IsKeyboardFocusWithin);
        Assert.True(frame.BorderThickness.Left > frame.BorderThickness.Right, "the selection is a thicker left edge, not a ring of another shade");
        Assert.Equal(before, Inset(frame));
        Frame(window, "needs-you-outlined-selected");
    });

    private static double Relative(Color c)
    {
        static double Lin(byte v) { var x = v / 255d; return x <= 0.03928 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4); }
        return (0.2126 * Lin(c.R)) + (0.7152 * Lin(c.G)) + (0.0722 * Lin(c.B));
    }

    private static double Ratio(Color a, Color b)
    {
        var (x, y) = (Relative(a), Relative(b));
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }

    private static Color FillOf(Border border) => (border.Background as ISolidColorBrush)?.Color ?? Colors.Transparent;

    private static (Window Window, Border FocusedOnly, Border Both) FocusedRows()
    {
        var focusedOnly = new Border { Classes = { "inbox-row" }, Width = 400, Height = 56, Child = new TextBlock { Text = "Row" } };
        var both = new Border { Classes = { "inbox-row", "selected" }, Width = 400, Height = 56, Child = new TextBlock { Text = "Row" } };
        var window = new Window { Content = new StackPanel { Children = { focusedOnly, both } }, Width = 480, Height = 240 };
        window.Show();
        ((IPseudoClasses)focusedOnly.Classes).Set(":focus-visible", value: true);
        ((IPseudoClasses)both.Classes).Set(":focus-visible", value: true);
        Settle(window);
        return (window, focusedOnly, both);
    }

    // #3506: a selected inbox row that also has keyboard focus (the usual case, selecting is the focus) lost its selected border, because the focus style
    // restates the border brush after the selected one. The selected fill alone is 1.18:1 on the canvas, so nothing marked the selection.
    // Both looks are pinned exactly (option 1, a 2 px ring): focused-only is a 1 px PrimaryHover border on the row's normal fill; selected and focused is a 2 px PrimaryHover
    // border on the Surface2 fill. A later edit cannot make them equal again, and the ring must reach 3:1 on the selected fill.
    [Fact]
    public Task A_selected_focused_row_still_shows_a_selected_marker() => OnUi(async () =>
    {
        Motion.Set(reduce: true);
        using var instant = InstantMotion.Start();
        var (window, focusedOnly, both) = FocusedRows();

        Assert.Equal((Brush("Brush.PrimaryHover"), new Thickness(1)), (BrushOf(focusedOnly), focusedOnly.BorderThickness));
        Assert.NotEqual(Brush("Brush.Surface2"), FillOf(focusedOnly));
        Assert.Equal((Brush("Brush.PrimaryHover"), new Thickness(2), Brush("Brush.Surface2")), (BrushOf(both), both.BorderThickness, FillOf(both)));
        Assert.True(Ratio(BrushOf(both), FillOf(both)) >= 3.0, string.Create(CultureInfo.InvariantCulture, $"the ring {BrushOf(both)} on the row fill {FillOf(both)} is {Ratio(BrushOf(both), FillOf(both)):0.00}:1, below 3:1"));
        window.Close();
        await Task.CompletedTask;
    });

    // The 2 px ring cuts the padding by 1 (15,7 against 16,8), so the content stays where it is. That holds only while the base border is 1 px.
    [Fact]
    public Task A_selected_focused_rows_content_sits_where_a_focused_only_rows_content_does() => OnUi(async () =>
    {
        Motion.Set(reduce: true);
        using var instant = InstantMotion.Start();
        var (window, focusedOnly, both) = FocusedRows();

        var plain = ((TextBlock)focusedOnly.Child!).TranslatePoint(default, focusedOnly);
        var marked = ((TextBlock)both.Child!).TranslatePoint(default, both);

        _ = Assert.NotNull(plain);
        Assert.Equal(plain, marked);
        window.Close();
        await Task.CompletedTask;
    });
}
