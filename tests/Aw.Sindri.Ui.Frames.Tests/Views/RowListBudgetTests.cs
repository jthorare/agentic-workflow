using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Controls.Rows;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// Perf's three budget checks for a 5,000-row RowList (#2978), counted in row containers and not in wall clock, so a loaded box does not flake them (#2946):
/// a scroll from top to bottom never holds more than two screens of rows, the first frame builds no more than that, and the whole scroll makes only a few screens of containers.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class RowListBudgetTests
{
    private const int Groups = 5;
    private const int PerGroup = 1000;
    private const double WindowHeight = 800;
    private const int Visible = 26;   // 800 px / 32 px rows, plus the partly shown one

    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(RowListBudgetTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public async Task A_scroll_from_top_to_bottom_never_holds_more_than_two_screens_of_rows() => await OnUi(() =>
    {
        var list = new RowList { ItemsSource = RowGallery.Many(Groups, PerGroup), Columns = RowGallery.Columns };
        var window = new Window { Content = list, Width = 1280, Height = WindowHeight };
        window.Show();
        Settle(window);
        var total = (Groups * PerGroup * 32d) + (Groups * 28d);

        var worst = 0;
        for (var offset = 0d; offset <= total; offset += 320)
        {
            list.ScrollTo(offset);
            Settle(window);
            worst = Math.Max(worst, list.RealizedRowCount);
        }

        Assert.True(worst <= 2 * Visible, string.Create(CultureInfo.InvariantCulture, $"{worst} rows realized at once, budget {2 * Visible}"));
        window.Close();
        return true;
    });

    [Fact]
    public async Task The_first_frame_builds_no_more_than_two_screens_of_row_containers() => await OnUi(() =>
    {
        var list = new RowList { ItemsSource = RowGallery.Many(Groups, PerGroup), Columns = RowGallery.Columns };
        var window = new Window { Content = list, Width = 1280, Height = WindowHeight };

        window.Show();   // no RunJobs and no UpdateLayout: what Show builds is the first frame

        Assert.True(list.ContainersCreated <= 2 * Visible, string.Create(CultureInfo.InvariantCulture, $"{list.ContainersCreated} containers before the first frame, budget {2 * Visible}"));
        window.Close();
        return true;
    });

    [Fact]
    public async Task The_whole_scroll_makes_a_few_screens_of_containers_and_not_5000() => await OnUi(() =>
    {
        var list = new RowList { ItemsSource = RowGallery.Many(Groups, PerGroup), Columns = RowGallery.Columns };
        var window = new Window { Content = list, Width = 1280, Height = WindowHeight };
        window.Show();
        Settle(window);
        var total = (Groups * PerGroup * 32d) + (Groups * 28d);

        for (var offset = 0d; offset <= total; offset += 320)
        {
            list.ScrollTo(offset);
            Settle(window);
        }

        Assert.True(list.ContainersCreated <= 4 * Visible, string.Create(CultureInfo.InvariantCulture, $"{list.ContainersCreated} containers made over the scroll, budget {4 * Visible}"));
        window.Close();
        return true;
    });
}
