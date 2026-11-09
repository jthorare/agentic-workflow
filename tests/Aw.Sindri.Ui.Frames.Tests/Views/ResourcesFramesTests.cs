using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Resources;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Resources;
using Aw.Sindri.Ui.Views.Resources;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>Key frames for the Resources page at 1440 and 900 px wide, hosted directly: a disk with its bar and a database, the purge confirm open, the empty page and a failed read with Try again. Saved under frames/ as resources-*.png.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ResourcesFramesTests : FrameHost
{
    private const string Prefix = "resources-";

    private static readonly Uri _url = new("http://127.0.0.1:7410/");

    private async Task OnPage(double width, Action<FakeSindriApi> seed, Func<Window, ResourcesPageViewModel, Task> body)
    {
        await OnUi(async () =>
        {
            var server = new FakeSindriApi();
            seed(server);
            var page = new ResourcesPageViewModel(server);
            await page.Refresh(_url, Token);
            var window = new Window { Width = width, Height = 900, Content = new ScrollViewer { Content = new ResourcesPage { DataContext = page } } };
            window.Show();
            try
            {
                Settle(window);
                await body(window, page);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void WithRows(FakeSindriApi server) => server.SeedResources(
        new ResourceView("workflow:docs", "disk", "scratch", 1_258_291, "/state/docs", LimitBytes: 5_242_880),
        new ResourceView("workflow:triage", "database", "triage.db", 2_411_724, "/state/triage.db"));

    [Theory]
    [InlineData(1440)]
    [InlineData(900)]
    public Task A_disk_with_its_bar_and_a_database(double width) => OnPage(width, WithRows, (window, _) =>
    {
        Assert.Equal("2 resources", Named<TextBlock>(window, "ResourcesHead").Text);
        Assert.True(Visible(window, "ResourceFill"));
        Assert.False(Visible(window, "ResourceConfirm"));
        Frame(window, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"list-{width}"), Prefix);
        return Task.CompletedTask;
    });

    [Theory]
    [InlineData(1440)]
    [InlineData(900)]
    public Task The_purge_confirm_open_on_a_row(double width) => OnPage(width, WithRows, (window, _) =>
    {
        Named<Button>(window, "ResourcePurge").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);

        Assert.True(Visible(window, "ResourceConfirm"));
        Frame(window, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"confirm-{width}"), Prefix);
        return Task.CompletedTask;
    });

    [Fact]
    public Task The_empty_page() => OnPage(1440, _ => { }, (window, _) =>
    {
        Assert.True(Visible(window, "ResourcesEmptyName"));
        Frame(window, "empty", Prefix);
        return Task.CompletedTask;
    });

    [Fact]
    public Task A_failed_read_with_Try_again() => OnPage(1440, server => server.ListResourcesError = new Error("SND-UI-002", ErrorKind.Unavailable, "no answer"), (window, _) =>
    {
        Assert.True(Visible(window, "ResourcesRetry"));
        Frame(window, "failed", Prefix);
        return Task.CompletedTask;
    });
}
