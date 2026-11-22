using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Projects;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Projects;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>
/// The notes area in the project's sheet (issue 3717: it moved there from the card when the rows replaced the cards): the buttons are named with the project,
/// Edit notes keeps its automation id, and Enter, Space or a press inside the box never reach the sheet around it.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ProjectNotesViewTests : FrameHost
{
    // The Projects page read, then the sheet of Checkout rewrite opened from its row; the notes area the sheet draws for it.
    private async Task<(Window Window, ShellViewModel Shell, ProjectCardViewModel Row, ProjectNotesEditor Notes)> SheetNotes()
    {
        var (window, shell) = await Connected();
        shell.GoTo(PageId.Projects);
        await Until(window, () => !shell.Projects.IsLoading, "the Projects page never read the list");
        var row = shell.Projects.Groups.SelectMany(g => g.Cards).First(c => c.Name == "Checkout rewrite");
        shell.Projects.RequestOpen(row.Link);
        await Until(window, () => shell.ProjectSheet.IsShown && window.GetVisualDescendants().OfType<ProjectNotesEditor>().Any(n => n.IsEffectivelyVisible), "the sheet never drew its notes area");
        return (window, shell, row, window.GetVisualDescendants().OfType<ProjectNotesEditor>().First(n => n.IsEffectivelyVisible));
    }

    [Fact]
    public Task Notes_buttons_have_names_with_project() => OnUi(async () =>
    {
        var (window, _, row, notes) = await SheetNotes();

        Assert.Equal("Edit notes for Checkout rewrite", Heard.Name(Named<Button>(notes, "ProjectNotesEdit")));

        row.BeginEdit();
        await Until(window, () => Visible(notes, "ProjectNotesSave"), "the editor never showed");
        Assert.EndsWith(" for Checkout rewrite", Heard.Name(Named<Button>(notes, "ProjectNotesSave")), StringComparison.Ordinal);
        Assert.EndsWith(" for Checkout rewrite", Heard.Name(Named<Button>(notes, "ProjectNotesCancel")), StringComparison.Ordinal);
    });

    // Guard: drivers and the perf inventory find the button by its id.
    [Fact]
    public Task Notes_edit_button_keeps_its_automation_id() => OnUi(async () =>
    {
        var (_, _, _, notes) = await SheetNotes();

        Assert.Equal("ProjectNotesEdit", ControlAutomationPeer.CreatePeerForElement(Named<Button>(notes, "ProjectNotesEdit")).GetAutomationId());
    });

    [Fact]
    public Task Enter_and_a_press_inside_the_notes_box_stop_at_the_notes_area() => OnUi(async () =>
    {
        var (window, _, row, notes) = await SheetNotes();
        row.BeginEdit();
        var sheet = notes.GetVisualAncestors().OfType<ProjectSheetView>().First();
        var seen = new List<string>();
        sheet.AddHandler(InputElement.KeyDownEvent, (_, _) => seen.Add("key"), RoutingStrategies.Bubble);
        sheet.AddHandler(InputElement.PointerPressedEvent, (_, _) => seen.Add("press"), RoutingStrategies.Bubble);
        await Until(window, () => Visible(notes, "ProjectNotesBox"), "the notes box never showed");
        var box = Named<TextBox>(notes, "ProjectNotesBox");

        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = box });
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space, Source = box });
        box.RaiseEvent(new PointerPressedEventArgs(box, new Pointer(0, PointerType.Mouse, isPrimary: true), box, default, 0, new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));

        Assert.Empty(seen);
    });
}
