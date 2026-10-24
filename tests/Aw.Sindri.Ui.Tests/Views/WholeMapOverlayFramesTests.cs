using Avalonia.Controls;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// The whole map's not-ready overlays in the window, on context's goldens (#866: building-in-progress, no-map-030, no-map-approved): Building with its counts and
/// job button, No map with its repository count, and a first build that has started shown as Building. Saved under frames/ as overlay-*.png when AW_UI_FRAMES=1.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class WholeMapOverlayFramesTests : FrameHost
{
    private async Task<(ShellWindow Window, ShellViewModel Shell)> Open(string scenario)
    {
        Server.WholeMapAnswer = _ => RouteAnswer.Problem("context-whole", scenario);
        var (window, shell) = await Connected(width: 1440, height: 900);
        shell.GoTo(PageId.Context);
        await Until(window, () => shell.Context.ShowEmpty || shell.Context.ShowBuilding);
        Settle(window);
        return (window, shell);
    }

    [Fact]
    public Task Building_says_how_far_the_build_is_and_its_job_button_opens_the_job() => OnUi(async () =>
    {
        var (window, shell) = await Open("building-in-progress");

        Assert.True(Visible(window, "ContextBuilding"));
        Assert.False(Visible(window, "ContextEmpty"));
        Assert.Equal(shell.Context.BuildingTitle, Named<TextBlock>(window, "ContextBuildingTitle").Text);
        Assert.StartsWith("Building the map · 1 of 1 source · started ", Named<TextBlock>(window, "ContextBuildingTitle").Text, StringComparison.Ordinal);
        Assert.StartsWith("last map ", Named<TextBlock>(window, "ContextBuildingBody").Text, StringComparison.Ordinal);
        Assert.True(Visible(window, "ContextBuildingJob"));
        Frame(window, "building-1440", "overlay-");

        shell.Context.RequestBuildJob();

        Assert.Equal(new DeepLink(DeepLinkKind.Job, "JOB-00000000000000000000000000000011"), shell.SheetLink);
    });

    [Fact]
    public Task No_map_names_the_repositories_and_keeps_the_build_button() => OnUi(async () =>
    {
        var (window, shell) = await Open("no-map-030");

        Assert.True(Visible(window, "ContextEmpty"));
        Assert.False(Visible(window, "ContextBuilding"));
        Assert.Equal("No map yet for 1 repository", Named<TextBlock>(window, "ContextEmptyTitle").Text);
        Assert.True(Visible(window, "ContextBuild"));
        Assert.NotNull(shell.Context.BuildControl);
        Frame(window, "no-map-1440", "overlay-");
    });

    [Fact]
    public Task A_first_build_that_has_started_draws_Building_without_counts() => OnUi(async () =>
    {
        var (window, _) = await Open("no-map-approved");

        Assert.True(Visible(window, "ContextBuilding"));
        Assert.False(Visible(window, "ContextEmpty"));
        Assert.StartsWith("Building the map · started ", Named<TextBlock>(window, "ContextBuildingTitle").Text, StringComparison.Ordinal);
        Assert.False(Visible(window, "ContextBuildingJob"));
        Frame(window, "first-build-1440", "overlay-");
    });
}
