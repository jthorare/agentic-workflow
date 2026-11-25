using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.Views.Linear;

namespace Aw.Sindri.Ui.Tests.Linear;

/// <summary>With filters set and no matching rows the Linear page shows one Clear filters, the one by the filters (#4440).</summary>
[Lane(TestLane.Integration)]
public sealed class LinearWorkClearFiltersTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_filtered_view_with_no_matches_shows_one_clear_filters_by_the_filters()
    {
        var f = new LinearFixture();
        var work = await f.Shown(Token);
        f.Mine = LinearFixture.Nothing("mine");
        f.Teams = LinearFixture.Nothing("teams");
        work.Filter("team").Pick("ENG");
        await work.Reading;

        var visible = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(LinearWorkClearFiltersTests).Assembly).Dispatch(() =>
        {
            var view = new LinearWorkView { DataContext = work };
            var window = new Window { Width = 1200, Height = 800, Content = view };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.True(work.ShowsNoMatch);
                return view.GetVisualDescendants().OfType<Button>()
                    .Where(b => b.IsEffectivelyVisible && string.Equals(b.Content as string, Labels.LinearClearFilters, StringComparison.Ordinal))
                    .Select(b => b.Name ?? string.Empty)
                    .ToArray();
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }
        }, Token);

        Assert.Equal(["LinearClearFilters"], visible);
    }
}
