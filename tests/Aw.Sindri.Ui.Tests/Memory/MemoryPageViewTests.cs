using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Memory;
using Aw.Sindri.Ui.Views.Memory;

namespace Aw.Sindri.Ui.Tests.Memory;

/// <summary>The Memory page in a window: the store list, a search with its mode chip and note read by assistive tech, a forget that keeps the row on an error, and no action beyond the memory tools.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class MemoryPageViewTests : FrameHost
{
    private readonly MemoryFixture _f = new();

    private async Task OnPage(Func<Window, MemoryPageViewModel, Task> body) => await OnUi(async () =>
    {
        var page = await _f.Shown(Token);
        var window = new Window { Width = 1200, Height = 900, Content = new ScrollViewer { Content = new MemoryPage { DataContext = page } } };
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

    private static void Click(Window window, string name) => Named<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static async Task OpenAndSearch(Window window, string query)
    {
        Click(window, "MemoryStoreToggle");
        Settle(window);
        Named<TextBox>(window, "MemorySearchBox").Text = query;
        Click(window, "MemorySearchButton");
        await Until(window, () => Visible(window, "MemoryModeChip"), "the search never answered");
    }

    private static List<Border> FactRows(Window window) =>
        [.. window.GetVisualDescendants().OfType<Border>().Where(b => b.Name == "MemoryFactFrame" && b.IsEffectivelyVisible)];

    [Fact]
    public Task The_list_names_a_store_with_its_fact_count() => OnPage((window, page) =>
    {
        var frame = Named<Border>(window, "MemoryStoreFrame");

        Assert.Equal("team-notes, 4 facts", Heard.Name(frame));
        Assert.Equal("1 store · 4 facts", Named<TextBlock>(window, "MemoryHead").Text);
        Assert.False(Visible(window, "MemoryStoreBody"));
        _ = Assert.Single(page.Stores);
        return Task.CompletedTask;
    });

    [Fact]
    public Task A_keyword_search_shows_its_chip_and_the_note_to_assistive_tech() => OnPage(async (window, page) =>
    {
        _f.Server.SearchMode = "keyword";
        _f.Server.SearchNote = "The embedding model is off, so recall used keywords";

        await OpenAndSearch(window, "Deploys");

        Assert.Equal("Keyword match", Heard.Wrapper(Named<Border>(window, "MemoryModeChip")));
        Assert.Equal(page.Stores[0].Search.NoteText, Heard.Wrapper(Named<Border>(window, "MemoryNoteName")));
        Assert.Equal(2, FactRows(window).Count);
        Assert.StartsWith("Deploys go out on Tuesdays", Heard.Name(FactRows(window)[0]), StringComparison.Ordinal);
    });

    [Fact]
    public Task A_forget_takes_the_row_out_once_the_API_answers() => OnPage(async (window, page) =>
    {
        await OpenAndSearch(window, "Deploys");

        Click(window, "MemoryFactForget");
        await Until(window, () => FactRows(window).Count == 1, "the forgotten row never left the list");

        Assert.Equal("f2", page.Stores[0].Search.Rows.Single().Key);
        Assert.True(Visible(window, "MemoryForgetResultName"));
    });

    [Fact]
    public Task A_failed_forget_keeps_the_row_and_shows_the_error() => OnPage(async (window, page) =>
    {
        await OpenAndSearch(window, "Deploys");
        _f.Server.ForgetError = new Error("SND-MEM-500", ErrorKind.Unavailable, "The store is locked.");

        Click(window, "MemoryFactForget");
        await Until(window, () => Visible(window, "MemoryForgetResultName"), "the error never showed");

        Assert.Equal(2, FactRows(window).Count);
        Assert.Contains("The store is locked.", Heard.Wrapper(Named<Border>(window, "MemoryForgetResultName")), StringComparison.Ordinal);
    });

    [Fact]
    public Task Create_adds_a_row_to_the_list() => OnPage(async (window, page) =>
    {
        Named<TextBox>(window, "MemoryNewName").Text = "runbooks";

        Click(window, "MemoryCreateButton");
        await Until(window, () => page.Stores.Count == 2, "the new store never joined the list");

        Assert.Equal("runbooks", page.Stores[0].Name);
    });

    [Fact]
    public Task The_page_offers_the_memory_tools_and_nothing_else() => OnPage(async (window, _) =>
    {
        await OpenAndSearch(window, "Deploys");

        var buttons = window.GetVisualDescendants().OfType<Button>().Select(b => b.Name).Where(n => n is not null && n.StartsWith("Memory", StringComparison.Ordinal)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

        // memory_list (the page), memory_create, memory_search, memory_write and memory_forget in three forms: no edit and no delete of a store.
        Assert.Equal(
            ["MemoryCreateButton", "MemoryFactForget", "MemoryForgetExpired", "MemorySearchButton", "MemoryStoreToggle", "MemoryTagForget", "MemoryWriteButton"],
            buttons);
    });
}
