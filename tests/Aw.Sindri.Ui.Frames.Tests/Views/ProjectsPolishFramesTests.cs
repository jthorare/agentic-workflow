using System.Net;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The Projects polish (#3719): a quiet Linear line with Connect instead of a red box, state chips on the sheet, and the note in the one edit form.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ProjectsPolishFramesTests : FrameHost
{
    private const string Attached = "prj_00000000000000000000000008";

    private static bool Shown(Window window, string name) =>
        window.GetVisualDescendants().OfType<Control>().Any(c => string.Equals(c.Name, name, StringComparison.Ordinal) && c.IsEffectivelyVisible);

    private async Task<(ShellWindow Window, ShellViewModel Shell)> OnProjects()
    {
        var (window, shell) = await Connected(width: 1440, height: 900);
        shell.GoTo(PageId.Projects);
        await Until(window, () => !shell.Projects.IsLoading, "the Projects page never read the list");
        return (window, shell);
    }

    [Fact]
    public Task Linear_not_connected_shows_one_quiet_line_with_Connect_and_no_red_box_3719() => OnUi(async () =>
    {
        var list = JsonNode.Parse(GoldenFiles.Api("projects", "mixed"))!;
        list["empty"] = "Linear isn't set up.";
        list["emptyCode"] = "linear-missing";
        Server.ProjectsAnswer = new RouteAnswer(HttpStatusCode.OK, list.ToJsonString());

        var (window, _) = await OnProjects();

        Assert.Equal((true, true, false), (Shown(window, "ProjectsLinearNoteText"), Shown(window, "ProjectsConnectNote"), Shown(window, "ProjectsBanner")));
    });

    [Fact]
    public Task The_sheet_shows_a_row_of_state_chips_and_no_Set_state_button_3719() => OnUi(async () =>
    {
        var (window, shell) = await OnProjects();
        shell.Projects.RequestOpen(new DeepLink(DeepLinkKind.Project, Uri.EscapeDataString(Attached)));
        await Until(window, () => shell.ProjectSheet.IsShown, "the project sheet never showed");

        var chips = window.GetVisualDescendants().OfType<Button>().Where(b => string.Equals(b.Name, "ProjectStateChip", StringComparison.Ordinal) && b.IsEffectivelyVisible).ToList();

        Assert.Equal((6, false), (chips.Count, Shown(window, "ProjectSetState")));
    });

    [Fact]
    public Task The_edit_form_holds_the_note_beside_the_other_fields_3719() => OnUi(async () =>
    {
        var (window, shell) = await OnProjects();
        shell.Projects.RequestOpen(new DeepLink(DeepLinkKind.Project, Uri.EscapeDataString(Attached)));
        await Until(window, () => shell.ProjectSheet.IsShown, "the project sheet never showed");

        shell.ProjectSheet.BeginEdit();
        Settle(window);

        Assert.True(Shown(window, "ProjectEditNotes"));
    });
}
