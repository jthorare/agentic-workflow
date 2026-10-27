using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>UX pass C-8: a filter that matches nothing says so and names the filter; "Nothing in this folder." stays for a folder that is empty, and the counts say how much of the folder the filter shows.</summary>
[Lane(TestLane.Unit)]
public sealed class FileMapFilterWordsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<FileMapViewModel> Filtered(string text)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var files = new FileMapViewModel(new ContextPorts(new FakeSindriApi(), () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
        await files.Load("src/tabs/", Token);
        files.FilterText = text;
        clock.Advance(FileMapViewModel.FilterDelay);
        for (var i = 0; i < 300 && files.FilterRuns == 0; i++)
        {
            await Task.Delay(10, Token);
        }

        return files;
    }

    [Fact]
    public async Task A_filter_with_no_match_says_so_and_is_not_an_empty_folder()
    {
        var files = await Filtered("zzzz");

        Assert.Equal((true, false), (files.IsNoMatch, files.IsEmptyFolder));
        Assert.Equal(Labels.ContextFilesNoMatch("zzzz"), files.NoMatchWord);
        Assert.Contains("zzzz", files.NoMatchWord, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_filter_that_matches_shows_how_many_of_the_folder_it_shows()
    {
        var files = await Filtered("Tab");

        Assert.False(files.IsNoMatch);
        Assert.Equal(Labels.ContextFilesFilteredCount(files.Rows.Count, files.Loaded), files.ShownTotalsText);
    }

    [Fact]
    public async Task Without_a_filter_the_totals_line_is_the_folders_as_served()
    {
        var files = await Filtered(string.Empty);

        Assert.Equal(files.TotalsText, files.ShownTotalsText);
    }
}
