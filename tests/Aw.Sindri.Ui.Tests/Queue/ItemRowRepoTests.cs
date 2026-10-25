using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Queue;

namespace Aw.Sindri.Ui.Tests.Queue;

/// <summary>The Repo column never draws a blank cell (UX pass C-13, #1083): an item with no repo shows a dash.</summary>
[Lane(TestLane.Unit)]
public sealed class ItemRowRepoTests
{
    private static ItemRowViewModel Row(string? repo) =>
        new(new ItemSummaryView(A.NewItem, "A title", repo, "new", Step: null, A.Noon, PrimaryAction: null, Actions: [], Owner: null), "queued", primaryAction: null, A.Noon);

    [Fact]
    public void An_item_with_no_repo_shows_a_dash_and_keeps_Repo_empty()
    {
        var row = Row(repo: null);

        Assert.Equal((string.Empty, Labels.NoRepo), (row.Repo, row.RepoShown));
    }

    [Fact]
    public void An_item_with_a_repo_shows_it()
    {
        Assert.Equal("agentic-workflow", Row("agentic-workflow").RepoShown);
    }
}
