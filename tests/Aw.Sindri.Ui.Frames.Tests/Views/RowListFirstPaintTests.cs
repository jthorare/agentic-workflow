using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Controls.Rows;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// The RowList's first paint in wall clock (#2978). Heavy lane: it is one first paint with no warm-up and no median, so a loaded box flakes it (1538 ms at load 45 on 16 cores);
/// run it by hand on a quiet box. <see cref="RowListBudgetTests"/> keeps the count side in integration: the first frame builds no more than two screens of row containers.
/// </summary>
[Lane(TestLane.Heavy)]
[Collection(HeadlessWindows.Name)]
public sealed class RowListFirstPaintTests
{
    [Fact]
    public async Task The_first_paint_of_5000_rows_fits_Homes_100_ms_budget_with_room_for_a_loaded_box() => await RowListBudgetTests.OnUi(() =>
    {
        var clock = Stopwatch.StartNew();
        var list = new RowList { ItemsSource = RowGallery.Many(RowListBudgetTests.Groups, RowListBudgetTests.PerGroup), Columns = RowGallery.Columns };
        var window = new Window { Content = list, Width = 1280, Height = RowListBudgetTests.WindowHeight };
        window.Show();
        RowListBudgetTests.Settle(window);
        clock.Stop();

        Assert.True(list.ContainersCreated > 0);
        Assert.True(
            clock.Elapsed.TotalMilliseconds <= 100 * RowListBudgetTests.Loose,
            string.Create(CultureInfo.InvariantCulture, $"first paint took {clock.Elapsed.TotalMilliseconds:0} ms, budget {100 * RowListBudgetTests.Loose:0} ms (100 ms x {RowListBudgetTests.Loose})"));
        window.Close();
        return true;
    });
}
