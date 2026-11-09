using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Memory;
using Aw.Sindri.Ui.Views.Memory;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>Key frames for the Memory page at 1440 and 900 px wide, hosted directly: the store list, a keyword search with its note, and a failed forget. Saved under frames/ as memory-*.png.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class MemoryFramesTests : FrameHost
{
    private const string Prefix = "memory-";

    private static readonly Uri _url = new("http://127.0.0.1:7410/");

    private async Task OnPage(double width, Func<Window, FakeSindriApi, Task> body) => await OnUi(async () =>
    {
        var server = new FakeSindriApi();
        _ = server.SeedMemory(
            "team-notes",
            FakeSindriApi.FactRow("f1", "Deploys go out on Tuesdays", ["deploys"]),
            FakeSindriApi.FactRow("f2", "Deploys need a second reviewer", ["deploys", "review"], new DateTimeOffset(2100, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        _ = server.SeedMemory("wiki");
        var page = new MemoryPageViewModel(server, new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01T00:05:00Z", System.Globalization.CultureInfo.InvariantCulture)));
        page.SetShown(shown: true);
        await page.Refresh(_url, Token);
        var window = new Window { Width = width, Height = 900, Content = new ScrollViewer { Content = new MemoryPage { DataContext = page } } };
        window.Show();
        try
        {
            Settle(window);
            await body(window, server);
        }
        finally
        {
            window.Close();
        }
    });

    private static async Task OpenAndSearch(Window window)
    {
        Named<Button>(window, "MemoryStoreToggle").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Settle(window);
        Named<TextBox>(window, "MemorySearchBox").Text = "Deploys";
        Named<Button>(window, "MemorySearchButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(window, () => Visible(window, "MemoryModeChip"), "the search never answered");
    }

    [Theory]
    [InlineData(1440)]
    [InlineData(900)]
    public Task The_store_list(double width) => OnPage(width, (window, _) =>
    {
        Assert.Equal("2 stores · 2 facts", Named<TextBlock>(window, "MemoryHead").Text);
        Assert.True(Visible(window, "MemoryStoreName"));
        Assert.False(Visible(window, "MemoryStoreBody"));
        Frame(window, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"list-{width}"), Prefix);
        return Task.CompletedTask;
    });

    [Theory]
    [InlineData(1440)]
    [InlineData(900)]
    public Task A_keyword_search_with_its_note(double width) => OnPage(width, async (window, server) =>
    {
        server.SearchMode = "keyword";
        server.SearchNote = "The embedding model is off, so recall used keywords";

        await OpenAndSearch(window);

        Assert.True(Visible(window, "MemoryNoteName"));
        Assert.True(Visible(window, "MemoryFactForget"));
        Frame(window, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"search-{width}"), Prefix);
    });

    [Fact]
    public Task A_failed_forget_keeps_the_row() => OnPage(1440, async (window, server) =>
    {
        await OpenAndSearch(window);
        server.ForgetError = new Error("SND-MEM-500", ErrorKind.Unavailable, "The store is locked.");

        Named<Button>(window, "MemoryFactForget").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Until(window, () => Visible(window, "MemoryForgetResultName"), "the error never showed");

        Assert.True(Visible(window, "MemoryFactText"));
        Frame(window, "forget-failed", Prefix);
    });
}
