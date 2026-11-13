using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Controls.Rows;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The row's focus and selection facts (#2978, qa on #3499): no app focus adorner on a row or a group header, and the selection bar stays visible under the cursor ring.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class RowFocusBarTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(RowFocusBarTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static (Window Window, RowList List) Show()
    {
        var list = new RowList { ItemsSource = RowGallery.Three(), Columns = RowGallery.Columns };
        var window = new Window { Content = list, Width = 1440, Height = 600 };
        window.Show();
        Settle(window);
        return (window, list);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    [Fact]
    public async Task A_focused_row_draws_nothing_outside_its_bounds() => await OnUi(() =>
    {
        var (window, list) = Show();
        var row = list.GetVisualDescendants().OfType<Row>().First(r => r.IsEffectivelyVisible && r.Bounds.Height > 0);

        Assert.True(row.Focus(NavigationMethod.Directional));
        Settle(window);
        Assert.Null(row.FocusAdorner);   // the app-wide 2 px adorner (Margin -4, clip off) would spill 4 px into the rows around it: the row draws its own ring inside its bounds
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_focused_group_header_draws_nothing_outside_its_bounds() => await OnUi(() =>
    {
        var (window, list) = Show();
        var header = list.GetVisualDescendants().OfType<RowGroupHeader>().First(h => h.Group is not null && h.Name != "PART_Sticky" && h.Bounds.Height > 0);

        Assert.True(header.Focus(NavigationMethod.Directional));
        Settle(window);
        Assert.Null(header.FocusAdorner);
        window.Close();
        return true;
    });

    private static RowGroupHeader FirstHeader(RowList list) =>
        list.GetVisualDescendants().OfType<RowGroupHeader>().First(h => h.Group is not null && h.Name != "PART_Sticky" && h.Bounds.Height > 0);

    private static (double Chevron, double Heading) ContentX(RowGroupHeader header)
    {
        var blocks = header.GetVisualDescendants().OfType<TextBlock>().ToList();   // chevron, heading, count, summary
        return (blocks[0].TranslatePoint(default, header)!.Value.X, blocks[1].TranslatePoint(default, header)!.Value.X);
    }

    [Fact]
    public async Task A_focused_group_header_draws_a_2px_ring_inside_its_bounds() => await OnUi(() =>
    {
        var (window, list) = Show();
        var header = FirstHeader(list);

        Assert.True(header.Focus(NavigationMethod.Directional));
        Settle(window);
        Assert.Equal(new Thickness(2), header.BorderThickness);
        Assert.True(header.TryFindResource("Brush.PrimaryHover", header.ActualThemeVariant, out var expected));
        var wanted = Assert.IsAssignableFrom<ISolidColorBrush>(expected).Color;
        Assert.Equal(wanted, Assert.IsAssignableFrom<ISolidColorBrush>(header.BorderBrush).Color);
        Assert.Null(header.FocusAdorner);   // nothing outside the bounds
        window.Close();
        return true;
    });

    [Fact]
    public async Task Group_header_content_x_is_the_same_focused_or_not() => await OnUi(() =>
    {
        var (window, list) = Show();
        var header = FirstHeader(list);
        var plain = ContentX(header);

        window.MouseMove(header.TranslatePoint(new Point(header.Bounds.Width / 2, header.Bounds.Height / 2), window)!.Value);
        Settle(window);
        var hovered = ContentX(header);
        Assert.True(header.Focus(NavigationMethod.Directional));
        Settle(window);
        var focused = ContentX(header);

        Assert.Equal(plain, hovered);
        Assert.Equal(plain, focused);
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_hovered_unfocused_group_header_has_no_ring() => await OnUi(() =>
    {
        var (window, list) = Show();
        var header = FirstHeader(list);

        window.MouseMove(header.TranslatePoint(new Point(header.Bounds.Width / 2, header.Bounds.Height / 2), window)!.Value);
        Settle(window);
        Assert.False(header.IsKeyboardFocusWithin);
        Assert.True(header.BorderThickness == default || header.BorderBrush is null || (header.BorderBrush is ISolidColorBrush b && b.Color.A == 0));
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_selected_focused_row_keeps_the_bar_inside_the_ring() => await OnUi(() =>
    {
        var (window, list) = Show();
        var row = list.GetVisualDescendants().OfType<Row>().First(r => r.IsEffectivelyVisible && r.Bounds.Height > 0);
        row.Classes.Add("sel");
        row.Classes.Add("cursor");
        Settle(window);
        var bar = Assert.NotNull(row.Layout.SelBar);
        var inner = Assert.NotNull(row.Layout.RingRect).Deflate(Row.RingStroke);   // inside the ring's 2 px stroke

        Assert.True(inner.Contains(bar.TopLeft) && inner.Contains(new Point(bar.Right - 0.01, bar.Bottom - 0.01)), string.Create(CultureInfo.InvariantCulture, $"bar {bar} is not inside {inner}"));
        window.Close();
        return true;
    });

    [Fact]
    public async Task Row_content_x_is_the_same_in_all_four_states() => await OnUi(() =>
    {
        var (window, list) = Show();
        var row = list.GetVisualDescendants().OfType<Row>().First(r => r.IsEffectivelyVisible && r.Bounds.Height > 0);
        var xs = new List<(double Title, double Cell, double Age)>();
        foreach (var classes in new[] { Array.Empty<string>(), ["sel"], ["cursor"], ["sel", "cursor"] })
        {
            row.Classes.Remove("sel");
            row.Classes.Remove("cursor");
            foreach (var c in classes)
            {
                row.Classes.Add(c);
            }

            Settle(window);
            xs.Add((row.Layout.TitleRect.X, row.Layout.Cells[0].Bounds.X, row.Layout.AgeRect.X));
        }

        Assert.All(xs, x => Assert.Equal(xs[0], x));   // plain, selected, focused, selected and focused
        window.Close();
        return true;
    });
}
