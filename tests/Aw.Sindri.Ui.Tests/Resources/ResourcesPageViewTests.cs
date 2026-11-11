using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Resources;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Resources;
using Aw.Sindri.Ui.Views.Resources;

namespace Aw.Sindri.Ui.Tests.Resources;

/// <summary>The Resources page in a window: the rows read by assistive tech, the confirm that names the owner, and the bar that only a resource with a limit has.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ResourcesPageViewTests : FrameHost
{
    private static readonly Uri _url = new("http://127.0.0.1:7410/");

    private readonly FakeSindriApi _server = new();

    private static ResourceView Disk => new("workflow:docs", "disk", "scratch", 1_258_291, "/state/docs", LimitBytes: 5_242_880);

    private static ResourceView Database => new("workflow:triage", "database", "triage.db", 1024, "/state/triage.db");

    private async Task OnPage(ResourceView resource, Func<Window, Task> body) => await OnUi(async () =>
    {
        _server.SeedResources(resource);
        var page = new ResourcesPageViewModel(_server);
        await page.Refresh(_url, Token);
        var window = new Window { Width = 1200, Height = 900, Content = new ScrollViewer { Content = new ResourcesPage { DataContext = page } } };
        window.Show();
        try
        {
            Settle(window);
            await body(window);
        }
        finally
        {
            window.Close();
        }
    });

    private static void Click(Window window, string name) => Named<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [Fact]
    public Task The_rows_render_owner_kind_name_and_the_size_against_the_limit() => OnPage(Disk, window =>
    {
        Assert.Equal("workflow:docs", Named<TextBlock>(window, "ResourceOwner").Text);
        Assert.Equal(("disk", "scratch", "1.2 MB of 5 MB"), (Named<TextBlock>(window, "ResourceKind").Text, Named<TextBlock>(window, "ResourceName").Text, Named<TextBlock>(window, "ResourceSize").Text));
        Assert.Equal("workflow:docs, disk, scratch, 1.2 MB of 5 MB", Heard.Name(Named<Border>(window, "ResourceFrame")));
        Assert.Equal("1 resource", Named<TextBlock>(window, "ResourcesHead").Text);
        return Task.CompletedTask;
    });

    [Fact]
    public Task Purge_shows_the_confirm_naming_the_owner_and_Keep_hides_it() => OnPage(Database, window =>
    {
        Assert.False(Visible(window, "ResourceConfirm"));

        Click(window, "ResourcePurge");
        Settle(window);

        Assert.True(Visible(window, "ResourceConfirm"));
        Assert.Contains("workflow:triage", Named<TextBlock>(window, "ResourceConfirmText").Text, StringComparison.Ordinal);
        Assert.Equal("Purge now workflow:triage", Heard.Name(Named<Button>(window, "ResourcePurgeConfirm")));

        Click(window, "ResourcePurgeKeep");
        Settle(window);

        Assert.False(Visible(window, "ResourceConfirm"));
        return Task.CompletedTask;
    });

    [Fact]
    public Task The_bar_is_hidden_for_a_database() => OnPage(Database, window =>
    {
        Assert.False(Visible(window, "ResourceFill"));
        return Task.CompletedTask;
    });

    [Fact]
    public Task The_bar_shows_for_a_disk_with_a_limit() => OnPage(Disk, window =>
    {
        Assert.True(Visible(window, "ResourceFill"));
        return Task.CompletedTask;
    });
}
