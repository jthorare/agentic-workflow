using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Projects;

namespace Aw.Sindri.Ui.Views.Projects;

#pragma warning disable MA0182 // why: ProjectSheetView.axaml places it, which the analyzer cannot see
/// <summary>The owner's notes in the project's sheet: Edit notes opens the box with focus in it, Save and Cancel close it, Reload takes the newer notes after a 409.</summary>
internal sealed partial class ProjectNotesEditor : UserControl
{
    public ProjectNotesEditor()
    {
        InitializeComponent();
        // The boundary of the notes area: Enter, Space and a press are the area's own (a newline, a space, a click into the box or on Save), so they stop here.
        // Tab and the arrows pass: Tab is how focus reaches Save and Cancel.
        ProjectNotesArea.AddHandler(KeyDownEvent, StopKeys, RoutingStrategies.Bubble, handledEventsToo: true);
        ProjectNotesArea.AddHandler(PointerPressedEvent, StopPointer, RoutingStrategies.Bubble, handledEventsToo: true);
        ProjectNotesArea.AddHandler(PointerReleasedEvent, StopPointer, RoutingStrategies.Bubble, handledEventsToo: true);
        ProjectNotesArea.AddHandler(TappedEvent, StopPointer, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private static void StopKeys(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            e.Handled = true;
        }
    }

    private static void StopPointer(object? sender, RoutedEventArgs e) => e.Handled = true;

    private void OnEdit(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ProjectCardViewModel card)
        {
            card.BeginEdit();
            _ = ProjectNotesBox.Focus();
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => (DataContext as ProjectCardViewModel)?.CancelEdit();

    private void OnSave(object? sender, RoutedEventArgs e) => _ = (DataContext as ProjectCardViewModel)?.Save(CancellationToken.None);

    private void OnReload(object? sender, RoutedEventArgs e) => _ = (DataContext as ProjectCardViewModel)?.Reload(CancellationToken.None);
}
#pragma warning restore MA0182
