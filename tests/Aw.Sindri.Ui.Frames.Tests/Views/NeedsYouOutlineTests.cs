using Avalonia;
using Avalonia.Controls;
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
    public Task A_selected_outlined_row_keeps_the_selection_bar_colour() => OnUi(async () =>
    {
        Motion.Set(reduce: true);
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
