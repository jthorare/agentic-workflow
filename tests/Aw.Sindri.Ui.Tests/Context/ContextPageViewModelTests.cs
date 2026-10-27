using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Context.Concepts;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>The Context page's skeleton (U9a0): opened empty, no read made, and the four seams its stages fill.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextPageViewModelTests
{
    private static ContextPageViewModel Page(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        return new ContextPageViewModel(new ContextPorts(api, () => null, clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
    }

    [Fact]
    public void Opens_empty_with_no_map_words_and_makes_no_read_before_an_anchor()
    {
        var server = new FakeSindriApi();

        var page = Page(server);

        Assert.Equal((Labels.Context, string.Empty, false, string.Empty), (page.Title, page.Head, page.HasHead, page.AnchorText));
        Assert.Equal(0, server.ContextCalls);
    }

    [Fact]
    public void The_seams_are_present_and_typed()
    {
        var page = Page(new FakeSindriApi());

        _ = Assert.IsType<MapReviewViewModel>(page.Review);
        _ = Assert.IsType<FileMapViewModel>(page.FileMap);
        _ = Assert.IsType<NodeCardViewModel>(page.Node);
        _ = Assert.IsType<SourcesPanelViewModel>(page.Sources);
    }

    [Fact]
    public void Every_stage_shares_the_pages_ports()
    {
        var page = Page(new FakeSindriApi());

        Assert.All([page.Review.Ports, page.FileMap.Ports, page.Node.Ports, page.Sources.Ports], p => Assert.Same(page.Ports, p));
    }

    [Fact]
    public async Task The_shell_builds_the_ports_from_its_own_clipboard_clock_catalog_and_follow()
    {
        var records = new InMemoryEndpointRecords();
        records.Write(A.Record());
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var shell = new ShellViewModel(records, new FakeSindriApi(), clock, new Preferences(new InMemoryPreferencesStore()), clipboard);
        var url = new Uri("http://127.0.0.1:1/");
        var reads = new List<Uri>();

        shell.Context.Ports.Follow((u, _) =>
        {
            reads.Add(u);
            return Task.CompletedTask;
        });
        await shell.Health.RefreshViews(url, TestContext.Current.CancellationToken);

        Assert.Same(clipboard, shell.Context.Ports.Clipboard);
        Assert.Same(clock, shell.Context.Ports.Clock);
        Assert.Same(shell.Health.Catalog, shell.Context.Ports.Catalog);
        Assert.Equal([url], reads);
    }

    [Fact]
    public void Typing_in_the_anchor_box_notifies_and_reads_nothing()
    {
        var server = new FakeSindriApi();
        var page = Page(server);
        var changed = new List<string?>();
        page.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        page.AnchorText = "feature:x";

        Assert.Equal([nameof(ContextPageViewModel.AnchorText)], changed);
        Assert.Equal(0, server.ContextCalls);
    }

    [Fact]
    public async Task Up_while_drilled_leaves_the_drill_then_closes_the_concept()
    {
        var page = await ConceptPage.Shown(ConceptPage.Serving(AConcepts.Three()));
        await page.OpenConcept("concept:billing", TestContext.Current.CancellationToken);
        page.Whole.DrillInto(page.Whole.BandButtons[0].Band);
        Assert.True(page.Whole.IsDrilled);

        Assert.True(page.Up());

        Assert.False(page.Whole.IsDrilled);
        Assert.NotNull(page.OpenedConceptId);
        Assert.True(page.Up());
        Assert.Null(page.OpenedConceptId);
    }
}
