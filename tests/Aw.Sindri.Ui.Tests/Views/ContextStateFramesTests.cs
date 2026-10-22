using System.Net;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// The Context page's Files view and states (F3 spec 1, slice 4) in the window: the file rows on the golden, the empty and building
/// overlays with the build action, and the refusal banner. Saved under frames/ as f3-context-*.png when AW_UI_FRAMES=1.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ContextStateFramesTests : FrameHost
{
    private static string Anchor => (string)GoldenFiles.Node("context-lenses", "map-tabs")["anchor"]!["key"]!;

    private async Task<(ShellWindow Window, ShellViewModel Shell)> Open()
    {
        var (window, shell) = await Connected(width: 1440, height: 900);
        shell.GoTo(PageId.Context);
        Settle(window);
        return (window, shell);
    }

    private static async Task Ask(ShellViewModel shell, string anchor)
    {
        shell.Context.AnchorText = anchor;
        await shell.Context.Submit(Token);
    }

    [Fact]
    public Task The_files_view_draws_36_px_rows_and_hatches_the_suggested_one() => OnUi(async () =>
    {
        var (window, shell) = await Open();
        await shell.Context.FileMap.Load("src/tabs/", Token);

        shell.Context.ChooseView(files: true);
        await Until(window, () => Visible(window, "FileRows") && window.GetVisualDescendants().OfType<Border>().Any(b => b.Classes.Contains("file-row")), "the file rows never drew");
        Settle(window);

        var rows = window.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("file-row")).ToList();
        Assert.Equal(shell.Context.FileMap.Rows.Count, rows.Count);
        Assert.All(rows, r => Assert.Equal(36, r.Bounds.Height, precision: 1));
        Assert.Equal(shell.Context.FileMap.Rows.Count(r => r.IsSuggested), rows.Count(r => r.Classes.Contains("suggested")));
        Assert.Equal(shell.Context.FileMap.TotalsText, Named<TextBlock>(window, "FilesTotals").Text);
        Assert.True(Visible(window, "FilesNote"));
        Assert.False(Visible(window, "ContextEmpty"));
        Frame(window, "context-files-1440", "f3-");
    });

    [Fact]
    public Task The_served_no_map_answer_draws_the_empty_state_and_its_button_builds_the_map() => OnUi(async () =>
    {
        Server.LensesAnswer = _ => RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-CONTEXT-030", "no map yet (fix: sindri context map build)");
        var (window, shell) = await Open();

        await Ask(shell, Anchor);
        await Until(window, () => Visible(window, "ContextEmpty"), "the empty state never drew");
        Settle(window);

        Assert.Equal(Labels.ContextStateEmptyTitle, Named<TextBlock>(window, "ContextEmptyTitle").Text);
        Assert.False(Visible(window, "ContextProblem"));
        Assert.False(Visible(window, "ContextStack"));
        Frame(window, "context-empty-1440", "f3-");

        await shell.Context.BuildControl!.Press(Token);
        await Until(window, () => Visible(window, "ContextBuilding"), "the building state never drew");
        Settle(window);

        Assert.Equal(1, Server.BuildContextMapCalls);
        Assert.False(Visible(window, "ContextEmpty"));
        Assert.Equal(Labels.ContextStateBuilding, Named<TextBlock>(window, "ContextBuildingTitle").Text);
        Frame(window, "context-building-1440", "f3-");
    });

    [Fact]
    public Task A_refusal_that_is_not_no_map_is_the_banner_and_hides_the_canvas() => OnUi(async () =>
    {
        var (window, shell) = await Open();

        await Ask(shell, "feature:nobody.knows");
        await Until(window, () => Visible(window, "ContextProblem"), "the banner never drew");
        Settle(window);

        Assert.False(Visible(window, "ContextEmpty"));
        Assert.False(Visible(window, "ContextBuilding"));
        Assert.False(Visible(window, "ContextStack"));
        Frame(window, "context-refusal-1440", "f3-");
    });
}
