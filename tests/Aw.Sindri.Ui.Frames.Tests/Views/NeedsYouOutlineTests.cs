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
}
