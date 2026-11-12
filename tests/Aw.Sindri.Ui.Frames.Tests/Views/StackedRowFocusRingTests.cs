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

    private sealed record Snapshot(double Height, Thickness Border, Thickness Padding, int Shadows);

    private static Snapshot Take(Border row) =>
        new(row.Bounds.Height, row.BorderThickness, row.Padding, row.BoxShadow.Count);

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Focusing_a_stacked_row_changes_no_layout_and_draws_the_ring_inside_its_bounds(int index, double scale)
    {
        var (before, after, ring, bounds) = await OnUi(() =>
        {
            var resources = Application.Current!.Resources;
            TextScale.Apply(resources, scale);
            try
            {
                var rows = new List<Border>();
                var stack = new StackPanel();
                for (var i = 0; i < 3; i++)
                {
                    var row = new Border { Focusable = true, Child = new TextBlock { Text = $"Row {i}", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center }, MinHeight = 48 };
                    row.Classes.Add("item-row");
                    rows.Add(row);
                    stack.Children.Add(row);
                }

                var window = new Window { Width = 400, Height = 300, Content = stack };
                window.Show();
                window.UpdateLayout();
                var target = rows[index];
                var unfocused = Take(target);
                _ = target.Focus(NavigationMethod.Tab);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var focused = Take(target);
                var shadow = target.BoxShadow.Count == 1 ? target.BoxShadow[0] : default;
                var below = rows.Select(r => r.Bounds.Top).ToArray();
                window.Close();
                return (unfocused, focused, (shadow, target.IsFocused, target.Classes.Contains(":focus-visible")), below);
            }
            finally
            {
                TextScale.Apply(resources, 1.0);
            }
        });

        Assert.Equal(before.Height, after.Height);
        Assert.Equal(before.Border, after.Border);
        Assert.Equal(before.Padding, after.Padding);
        Assert.Equal(1, after.Shadows);
        Assert.True(ring.shadow.IsInset);
        Assert.Equal(2, ring.shadow.Spread);
        Assert.Equal(0, ring.shadow.Blur);
        Assert.Equal(Color.Parse("#828fff"), ring.shadow.Color);
        Assert.Equal(3, bounds.Length);
    }
}
