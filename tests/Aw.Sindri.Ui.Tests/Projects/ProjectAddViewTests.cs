using System.Net;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Projects;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>The Add project sheet in the window (projects page spec §3.2): the buttons that open it, its By hand form, and Esc.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ProjectAddViewTests : FrameHost
{
    private static readonly string[] _formControls = ["ProjectAddName", "ProjectAddState", "ProjectAddLinks", "ProjectAddNotes", "ProjectAddTarget", "ProjectAddSave", "ProjectAddCancel"];

    private async Task<(ShellWindow Window, ShellViewModel Shell)> OnProjects(string? scenario = null)
    {
        if (scenario is not null)
        {
            Server.ProjectsAnswer = RouteAnswer.Api("projects", scenario);
        }

        var (window, shell) = await Connected();
        shell.GoTo(PageId.Projects);
        await Until(window, () => !shell.Projects.IsLoading, "the Projects page never read the list");
        return (window, shell);
    }

    private static void Click(Window window, string name) =>
        Named<Button>(window, name).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

    [Fact]
    public Task The_head_button_opens_the_sheet_with_the_By_hand_form() => OnUi(async () =>
    {
        var (window, shell) = await OnProjects();

        Click(window, "ProjectsAdd");
        await Until(window, () => Visible(window, "ProjectAddForm"), "the Add project form never showed");

        Assert.Equal((true, "Add project"), (shell.IsProjectAddSheet, Named<TextBlock>(window, "SheetSubjectText").Text));
        Assert.All(_formControls, name => Assert.True(Visible(window, name), name));
        Assert.False(Named<Button>(window, "ProjectAddSave").IsEnabled);
    });

    [Fact]
    public Task The_empty_blocks_button_opens_the_sheet_too() => OnUi(async () =>
    {
        var (window, shell) = await OnProjects("empty-leads-nothing");

        Click(window, "ProjectsAddEmpty");

        await Until(window, () => shell.IsProjectAddSheet, "the sheet never opened");
    });

    [Fact]
    public Task Typing_a_name_enables_Save_and_Save_sends_it() => OnUi(async () =>
    {
        var (window, shell) = await OnProjects();
        Click(window, "ProjectsAdd");
        await Until(window, () => Visible(window, "ProjectAddForm"), "the Add project form never showed");

        Named<TextBox>(window, "ProjectAddName").Text = "Kitchen";
        Assert.True(Named<Button>(window, "ProjectAddSave").IsEnabled);
        Click(window, "ProjectAddSave");

        await Until(window, () => !shell.IsSheetOpen, "the sheet never closed after Save");
        Assert.Equal("Kitchen", Assert.IsType<ProjectAttach>(Assert.Single(Server.ProjectWrites).Body).Name);
    });

    [Fact]
    public Task The_tabs_switch_between_the_Linear_placeholder_and_the_form() => OnUi(async () =>
    {
        var (window, _) = await OnProjects();
        Click(window, "ProjectsAdd");
        await Until(window, () => Visible(window, "ProjectAddForm"), "the Add project form never showed");

        Click(window, "ProjectAddTabLinear");
        Assert.Equal((false, true), (Visible(window, "ProjectAddForm"), Visible(window, "ProjectAddLinear")));
        Click(window, "ProjectAddTabHand");
        Assert.Equal((true, false), (Visible(window, "ProjectAddForm"), Visible(window, "ProjectAddLinear")));
    });

    [Fact]
    public Task Esc_closes_the_sheet_and_focus_returns_to_Add_project() => OnUi(async () =>
    {
        var (window, shell) = await OnProjects();
        Click(window, "ProjectsAdd");
        await Until(window, () => Visible(window, "ProjectAddForm"), "the Add project form never showed");

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Settle(window);

        Assert.False(shell.IsSheetOpen);
        await Until(window, () => Named<Button>(window, "ProjectsAdd").IsFocused, "focus never went back to Add project");
    });

    [Fact]
    public Task A_refusal_shows_under_the_button() => OnUi(async () =>
    {
        var (window, _) = await OnProjects();
        Server.ProjectWriteAnswer = RouteAnswer.Problem(HttpStatusCode.BadRequest, "SND-PROJECT-004", "name: too long");
        Click(window, "ProjectsAdd");
        await Until(window, () => Visible(window, "ProjectAddForm"), "the Add project form never showed");
        Named<TextBox>(window, "ProjectAddName").Text = "Kitchen";

        Click(window, "ProjectAddSave");

        await Until(window, () => Visible(window, "ProjectAddError"), "the error never showed");
        Assert.Equal("Kitchen", Named<TextBox>(window, "ProjectAddName").Text);
    });
}
