using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Theme;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>#3245: the shared Border.item-row focus ring keeps the row's height. Rows sit 0 px apart, so a ring that changes BorderThickness or Padding
/// moves every row below it; the ring is one inset 2 px PrimaryHover shadow drawn inside the row's own bounds.</summary>
[Lane(TestLane.Integration)]
public sealed class StackedRowFocusRingTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(StackedRowFocusRingTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    public static TheoryData<int, double> Cases() => new()
    {
        { 0, 1.0 }, { 1, 1.0 }, { 2, 1.0 },
        { 0, 1.3 }, { 1, 1.3 }, { 2, 1.3 },
    };

    private static Color TokenColor(string key) =>
        Application.Current is { } app && app.TryFindResource(key, app.ActualThemeVariant, out var value) && value is ISolidColorBrush brush ? brush.Color : throw new InvalidOperationException($"{key} is not a brush token");

    private sealed record Snapshot(double Height, Thickness Border, Thickness Padding, int Shadows);

    private static Snapshot Take(Border row) =>
        new(row.Bounds.Height, row.BorderThickness, row.Padding, row.BoxShadow.Count);

    private static (List<Border> Rows, Window Window) ShowRows()
    {
        var rows = new List<Border>();
        var stack = new StackPanel();
        for (var i = 0; i < 3; i++)
        {
            var text = new TextBlock { Text = string.Create(CultureInfo.InvariantCulture, $"Row {i}"), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            var row = new Border { Focusable = true, Child = text, MinHeight = 48 };
            row.Classes.Add("item-row");
            rows.Add(row);
            stack.Children.Add(row);
        }

        var window = new Window { Width = 400, Height = 300, Content = stack };
        window.Show();
        window.UpdateLayout();
        return (rows, window);
    }

    private static (Snapshot Before, Snapshot After, BoxShadow Ring, Color TokenColour) FocusRow(int index)
    {
        var (rows, window) = ShowRows();
        var target = rows[index];
        var unfocused = Take(target);
        _ = target.Focus(NavigationMethod.Tab);
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        var focused = Take(target);
        var shadow = target.BoxShadow.Count == 1 ? target.BoxShadow[0] : default;
        window.Close();
        return (unfocused, focused, shadow, TokenColor("Brush.PrimaryHover"));
    }

    private static (Snapshot Before, Snapshot After, BoxShadow Ring, Color TokenColour) FocusRowAtScale(int index, double scale)
    {
        var resources = Application.Current!.Resources;
        TextScale.Apply(resources, scale);
        try
        {
            return FocusRow(index);
        }
        finally
        {
            TextScale.Apply(resources, 1.0);
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Focusing_a_stacked_row_changes_no_layout_and_draws_the_ring_inside_its_bounds(int index, double scale)
    {
        var (before, after, ring, tokenColour) = await OnUi(() => FocusRowAtScale(index, scale));

        Assert.Equal(before.Height, after.Height);
        Assert.Equal(before.Border, after.Border);
        Assert.Equal(before.Padding, after.Padding);
        Assert.Equal(1, after.Shadows);
        Assert.True(ring.IsInset);
        Assert.Equal(2, ring.Spread);
        Assert.Equal(0, ring.Blur);
        Assert.Equal(tokenColour, ring.Color);
    }
}
