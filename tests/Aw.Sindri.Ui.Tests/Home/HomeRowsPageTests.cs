using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Home;
using Aw.Sindri.Ui.Views.Controls.Rows;
using Aw.Sindri.Ui.Views.Home;

namespace Aw.Sindri.Ui.Tests.Home;

/// <summary>Home as rows under the headless platform (home spec §7.1, §7.3, U1): the five tabs with their counts, the rows of the selected tab, and an empty tab's one line.</summary>
[Lane(TestLane.Integration)]
public sealed class HomeRowsPageTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 12, 9, 10, 0, TimeSpan.Zero);

    private readonly List<Window> _windows = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task OnUi(Action body) =>
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(HomeRowsPageTests).Assembly)
            .Dispatch(() =>
            {
                try
                {
                    body();
                }
                finally
                {
                    _windows.ForEach(w => w.Close());
                    Dispatcher.UIThread.RunJobs();
                }

                return true;
            }, Token);

    private (HomeRowsPage Page, HomeRowsViewModel Home) Show(string scenario)
    {
        var home = new HomeRowsViewModel();
        home.Show(AwJson.Deserialize<HomeView>(Encoding.UTF8.GetBytes(GoldenFiles.Api("home", scenario)), SindriApiClient.Json).Value, _now);
        var page = new HomeRowsPage { DataContext = home };
        var window = new Window { Width = 1200, Height = 800, Content = page };
        _windows.Add(window);
        window.Show();
        Settle(window);
        return (page, home);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static List<ToggleButton> Tabs(HomeRowsPage page) => [.. page.GetVisualDescendants().OfType<ToggleButton>()];

    [Fact]
    public Task The_header_draws_the_five_tabs_with_their_counts_and_the_list_draws_the_rows() => OnUi(() =>
    {
        var (page, _) = Show("fixture");

        Assert.Equal(["All, 6", "Reviews, 3", "Messages, 1", "Work, 0", "Agents, 2"], Tabs(page).Select(t => Avalonia.Automation.AutomationProperties.GetName(t)), StringComparer.Ordinal);
        Assert.Equal([true, false, false, false, false], Tabs(page).Select(t => t.IsChecked == true));
        var list = page.GetVisualDescendants().OfType<RowList>().Single();
        Assert.True(list.IsVisible);
        Assert.Equal(6, list.RealizedRowCount);
    });

    [Fact]
    public Task A_click_on_a_tab_shows_its_rows_and_checks_it() => OnUi(() =>
    {
        var (page, home) = Show("fixture");

        Tabs(page)[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(_windows[0]);

        Assert.Equal(HomeSplit.Reviews, home.Split);
        Assert.Equal([false, true, false, false, false], Tabs(page).Select(t => t.IsChecked == true));
        Assert.Equal(3, page.GetVisualDescendants().OfType<RowList>().Single().RealizedRowCount);
    });

    [Fact]
    public Task A_tab_with_no_rows_shows_its_one_line_in_place_of_the_list() => OnUi(() =>
    {
        var (page, home) = Show("fixture");

        home.Select(HomeSplit.Work);
        Settle(_windows[0]);

        var line = page.GetVisualDescendants().OfType<TextBlock>().Single(t => string.Equals(t.Name, "HomeEmptyLine", StringComparison.Ordinal));
        Assert.Equal((true, "Nothing in Work"), (line.IsVisible, line.Text));
        Assert.False(page.GetVisualDescendants().OfType<RowList>().Single().IsVisible);
    });
}
