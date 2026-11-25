using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Controls.Rows;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The pointer path of the flat Row (#3189, review of #3499): a real move over a row fills it with Surface2 and shows the action strip, and a click on the strip's button runs its command.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class RowPointerTests
{
    [Fact]
    public async Task A_pointer_over_a_row_fills_it_with_Surface2_and_shows_the_strip_and_a_click_on_its_button_runs_the_command() =>
        await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(RowPointerTests).Assembly).Dispatch(() =>
        {
            var command = new CountingCommand();
            var list = new RowList { ItemsSource = new[] { RowGallery.Group("Needs you", 4, 0, i => RowGallery.Row(i, action: command)) }, Columns = RowGallery.Columns };
            var window = new Window { Content = list, Width = 1280, Height = 600 };
            window.Show();
            Settle(window);
            var row = list.GetVisualDescendants().OfType<Row>().Where(r => r.IsEffectivelyVisible && r.Bounds.Height > 0).OrderBy(r => r.TranslatePoint(default, window)?.Y ?? 0).ElementAt(1);
            Assert.Null(row.Strip);

            var over = row.TranslatePoint(new Point(300, row.Bounds.Height / 2), window) ?? throw new InvalidOperationException("The row is not in the window.");
            window.MouseMove(over, RawInputModifiers.None);
            Settle(window);

            var surface2 = Assert.IsType<ISolidColorBrush>(window.FindResource("Brush.Surface2"), exactMatch: false).Color;
            Assert.Equal(surface2, Assert.IsType<ISolidColorBrush>(row.Background, exactMatch: false).Color);
            var strip = Assert.IsType<RowActionsStrip>(row.Strip);
            var button = Assert.Single(strip.GetVisualDescendants().OfType<Button>());

            var at = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window) ?? throw new InvalidOperationException("The button is not in the window.");
            window.MouseMove(at, RawInputModifiers.None);
            window.MouseDown(at, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(at, MouseButton.Left, RawInputModifiers.None);
            Settle(window);

            Assert.Equal(1, command.Runs);
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken);

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }
}
